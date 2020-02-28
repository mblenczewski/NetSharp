using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetSharp.Deprecated;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Pipelines;
using NetSharp.Sockets;
using NetSharp.Utils;

namespace NetSharp
{
    /// <summary>
    /// Encapsulates a connection capable of receiving packets and responding to them with registered packet handlers.
    /// </summary>
    public class Connection : IDisposable
    {
        /// <summary>
        /// Represents any remote endpoint for datagram operations.
        /// </summary>
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly SocketAcceptor acceptor;

        private readonly ConcurrentDictionary<EndPoint, DatagramClientArgs> datagramConnections;
        private readonly Socket datagramSocket;
        private readonly Channel<(EndPoint origin, Memory<byte> packet)> incomingPacketChannel;

        /// <summary>
        /// Pipeline to convert incoming byte buffers to <see cref="NetworkPacket"/> instances.
        /// </summary>
        private readonly PacketPipeline<Memory<byte>, Memory<byte>, NetworkPacket> incomingPacketPipeline;

        private readonly SocketReader listener;
        private readonly Channel<(EndPoint destination, NetworkPacket packet)> outgoingPacketChannel;

        /// <summary>
        /// Pipeline to convert outgoing <see cref="NetworkPacket"/> instances to a byte buffer for sending.
        /// </summary>
        private readonly PacketPipeline<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPacketPipeline;

        private readonly Channel<(EndPoint origin, IRequestPacket request)> requestChannel;
        private readonly CancellationTokenSource serverShutdownTokenSource;
        private readonly ConcurrentDictionary<EndPoint, StreamClientArgs> streamConnections;

        private readonly Socket streamSocket;

        private readonly SocketWriter transmitter;

        /// <summary>
        /// Destroys a <see cref="Connection"/> class instance, freeing all managed resources.
        /// </summary>
        ~Connection()
        {
            Dispose(false);
        }

        private async Task AcceptorWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            logger.LogMessage("Started stream acceptor task.");

            async Task StreamListenerWork(object clientSocketObj)
            {
                Socket clientSocket = (Socket)clientSocketObj;
                logger.LogMessage("Started stream listener task.");

                while (!cancellationToken.IsCancellationRequested)
                {
                    // TODO: implement receive buffer pooling
                    byte[] receiveBuffer = new byte[NetworkPacket.PacketSize];
                    Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

                    TransmissionResult result =
                        await listener.ReceiveAsync(clientSocket, SocketFlags.None, receiveBufferMemory, cancellationToken);

                    await incomingPacketChannel.Writer.WriteAsync((result.RemoteEndPoint, receiveBufferMemory),
                        cancellationToken);
                }

                logger.LogMessage("Stopped stream listener task.");
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket clientSocket = await acceptor.AcceptAsync(streamSocket, cancellationToken);

                if (!streamConnections.ContainsKey(clientSocket.RemoteEndPoint))
                {
                    streamConnections[clientSocket.RemoteEndPoint] = new StreamClientArgs();
                }

                await Task.Factory.StartNew(StreamListenerWork, clientSocket, ServerShutdownToken,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            logger.LogMessage("Stopped stream acceptor task.");
        }

        private async Task DatagramListenerWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            logger.LogMessage("Started datagram listener task.");

            while (!cancellationToken.IsCancellationRequested)
            {
                // TODO: implement receive buffer pooling
                byte[] receiveBuffer = new byte[NetworkPacket.PacketSize];
                Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

                TransmissionResult result =
                    await listener.ReceiveFromAsync(datagramSocket, AnyRemoteEndPoint, SocketFlags.None,
                        receiveBufferMemory, cancellationToken);

                if (!datagramConnections.ContainsKey(result.RemoteEndPoint))
                {
                    datagramConnections[result.RemoteEndPoint] = new DatagramClientArgs();
                }

                await incomingPacketChannel.Writer.WriteAsync((result.RemoteEndPoint, receiveBufferMemory), cancellationToken);
            }

            logger.LogMessage("Stopped datagram listener task.");
        }

        private async Task IncomingPacketHandlerWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            logger.LogMessage("Started incoming packet handler task.");

            while (!cancellationToken.IsCancellationRequested)
            {
                (EndPoint origin, Memory<byte> packet) =
                    await incomingPacketChannel.Reader.ReadAsync(cancellationToken);

                NetworkPacket deserialisedRequest = incomingPacketPipeline.ProcessPacket(packet);

                // TODO implement deserialisation according to registered packet deserialisers

                // TODO: write to requestChannel, not to outgoingPacketChannel
                await outgoingPacketChannel.Writer.WriteAsync((origin, deserialisedRequest), cancellationToken);
            }

            logger.LogMessage("Stopped incoming packet handler task.");
        }

        private async Task OutgoingPacketHandlerWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            logger.LogMessage("Started outgoing packet handler task.");

            while (!cancellationToken.IsCancellationRequested)
            {
                (EndPoint destination, NetworkPacket packet) =
                    await outgoingPacketChannel.Reader.ReadAsync(cancellationToken);

                Memory<byte> serialisedResponse = outgoingPacketPipeline.ProcessPacket(packet);

                if (datagramConnections.ContainsKey(destination))
                {
                    await transmitter.SendToAsync(datagramSocket, destination,
                        SocketFlags.None, serialisedResponse, cancellationToken);
                }
                else if (streamConnections.ContainsKey(destination))
                {
                    StreamClientArgs streamClientArgs = streamConnections[destination];

                    await transmitter.SendAsync(streamClientArgs.ClientSocket,
                        SocketFlags.None, serialisedResponse, cancellationToken);
                }
                else
                {
                    logger.LogWarning($"Dropping packet destined for unknown destination; {destination}");
                }
            }

            logger.LogMessage("Stopped outgoing packet handler task.");
        }

        private async Task RequestHandlerInvocationWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            logger.LogMessage("Started request handler invocation task.");

            while (!cancellationToken.IsCancellationRequested)
            {
                (EndPoint origin, IRequestPacket request) = await requestChannel.Reader.ReadAsync(cancellationToken);

                // TODO: implement proper request handling, and conversion to IResponsePacket<IRequestPacke>

                NetworkPacket serialisedResponsePacket = new NetworkPacket();

                await outgoingPacketChannel.Writer.WriteAsync((origin, serialisedResponsePacket), cancellationToken);
            }

            logger.LogMessage("Stopped request handler invocation task.");
        }

        private readonly struct DatagramClientArgs
        {
            public readonly EndPoint ClientEndPoint;

            public DatagramClientArgs(EndPoint clientEndPoint)
            {
                ClientEndPoint = clientEndPoint;
            }
        }

        private readonly struct StreamClientArgs
        {
            public readonly Socket ClientSocket;

            public StreamClientArgs(Socket clientSocket)
            {
                ClientSocket = clientSocket;
            }
        }

        /// <summary>
        /// Lock synchronisation object for the <see cref="logger"/> variable.
        /// </summary>
        protected readonly object loggerLockObject = new object();

        /// <summary>
        /// Cancellation token which allows observing the shutdown of the server. It is set when <see cref="ShutdownServer"/> is called.
        /// </summary>
        protected readonly CancellationToken ServerShutdownToken;

        /// <summary>
        /// A logger object allowing for writing debug messages to an output stream.
        /// </summary>
        protected Logger logger;

        /// <summary>
        /// Disposes of the managed and unmanaged resources held by this instance.
        /// </summary>
        /// <param name="disposing">Whether this method is called by <see cref="Dispose()"/> or by the finaliser.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                streamSocket.Dispose();
            }
        }

        internal Connection(
                    PacketPipeline<Memory<byte>, Memory<byte>, NetworkPacket> incomingPacketPipeline,
                    PacketPipeline<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPacketPipeline,
                    int objectPoolSize = 10, bool preallocateBuffers = false, Stream? loggingStream = default,
                    LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            serverShutdownTokenSource = new CancellationTokenSource();
            ServerShutdownToken = serverShutdownTokenSource.Token;

            streamSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            datagramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            acceptor = new SocketAcceptor(objectPoolSize);
            listener = new SocketReader(NetworkPacket.PacketSize, objectPoolSize, preallocateBuffers);
            transmitter = new SocketWriter(NetworkPacket.PacketSize, objectPoolSize, preallocateBuffers);

            streamConnections = new ConcurrentDictionary<EndPoint, StreamClientArgs>();
            datagramConnections = new ConcurrentDictionary<EndPoint, DatagramClientArgs>();

            this.incomingPacketPipeline = incomingPacketPipeline;
            BoundedChannelOptions incomingChannelOptions = new BoundedChannelOptions(MaximumPacketBacklog)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            };
            incomingPacketChannel = Channel.CreateBounded<(EndPoint origin, Memory<byte> packet)>(incomingChannelOptions);

            BoundedChannelOptions requestChannelOptions = new BoundedChannelOptions(MaximumPacketBacklog)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            };
            requestChannel = Channel.CreateBounded<(EndPoint origin, IRequestPacket request)>(requestChannelOptions);

            this.outgoingPacketPipeline = outgoingPacketPipeline;
            BoundedChannelOptions outgoingChannelOptions = new BoundedChannelOptions(MaximumPacketBacklog)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
            };
            outgoingPacketChannel = Channel.CreateBounded<(EndPoint destination, NetworkPacket packet)>(outgoingChannelOptions);

            logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
        }

        /// <summary>
        /// The maximum number of packets that will be stored before older packets start to be dropped.
        /// </summary>
        /// TODO change this to a configurable builder option
        public const int MaximumPacketBacklog = 64;

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Task<TransmissionResult> ReceiveAsync(Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return listener.ReceiveAsync(streamSocket, flags, inputBuffer, cts.Token);
        }

        public Task<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return listener.ReceiveFromAsync(datagramSocket, remoteEndPoint, flags, inputBuffer, cts.Token);
        }

        /// <summary>
        /// Makes the connection listen for incoming request packets, and handle them according to registered packet handler delegates.
        /// This work can be cancelled by calling <see cref="ShutdownServer"/>.
        /// </summary>
        /// <returns>The task representing the connection work.</returns>
        public Task RunServerAsync()
        {
            logger.LogMessage("Starting stream acceptor task...");
            Task acceptorThread =
                Task.Factory.StartNew(AcceptorWork, ServerShutdownToken, ServerShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Result;

            logger.LogMessage("Starting datagram listener task...");
            Task datagramListenerThread =
                Task.Factory.StartNew(DatagramListenerWork, ServerShutdownToken, ServerShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            logger.LogMessage("Starting incoming packet handler task...");
            Task incomingPacketHandlerThread =
                Task.Factory.StartNew(IncomingPacketHandlerWork, ServerShutdownToken, ServerShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            logger.LogMessage("Starting request packet invocation task...");
            Task requestHandlerInvocationThread =
                Task.Factory.StartNew(RequestHandlerInvocationWork, ServerShutdownToken, ServerShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Result;

            logger.LogMessage("Starting outgoing packet handler task...");
            Task outgoingPacketHandlerThread =
                Task.Factory.StartNew(OutgoingPacketHandlerWork, ServerShutdownToken, ServerShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            return Task.WhenAll(acceptorThread, datagramListenerThread,
                incomingPacketHandlerThread, requestHandlerInvocationThread, outgoingPacketHandlerThread);
        }

        public ValueTask<int> SendAsync(Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return transmitter.SendAsync(streamSocket, flags, outputBuffer, cts.Token);
        }

        public ValueTask<int> SendToAsync(EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return transmitter.SendToAsync(datagramSocket, remoteEndPoint, flags, outputBuffer, cts.Token);
        }

        /// <summary>
        /// Configures the logger to log messages to the given stream (or to <see cref="Stream.Null"/> if <c>null</c>) and
        /// to only log messages that are of severity <paramref name="minimumLoggedSeverity"/> or higher.
        /// </summary>
        /// <param name="loggingStream">The stream to which messages will be logged.</param>
        /// <param name="minimumLoggedSeverity">The minimum severity a message must be to be logged.</param>
        public void SetLoggingStream(Stream? loggingStream, LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            lock (loggerLockObject)
            {
                logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
            }
        }

        /// <summary>
        /// Shuts down the connection, and releases managed and unmanaged resources.
        /// </summary>
        public void ShutdownServer()
        {
            logger.LogMessage("Signalling shutdown to all client connection handlers...");
            serverShutdownTokenSource.Cancel();
        }

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local endpoint. Blocks.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public bool TryBind(EndPoint localEndPoint, TimeSpan timeout) =>
            TryBindAsync(localEndPoint, timeout).Result;

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local endpoint. Does not block.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public async Task<bool> TryBindAsync(EndPoint localEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            try
            {
                return await Task.Run(() =>
                {
                    streamSocket.Bind(localEndPoint);
                    datagramSocket.Bind(localEndPoint);

                    return true;
                }, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on binding socket to {localEndPoint}:", ex);
                return false;
            }
        }
    }
}