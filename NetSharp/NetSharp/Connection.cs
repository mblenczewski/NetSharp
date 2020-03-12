using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Deprecated;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Pipelines;
using NetSharp.Utils;

namespace NetSharp
{
    /// <summary>
    /// Encapsulates a connection capable of receiving packets and responding to them with registered packet handlers.
    /// </summary>
    public sealed partial class Connection : IDisposable
    {
        private readonly HashSet<EndPoint> datagramConnections;
        private readonly Channel<(EndPoint origin, Memory<byte> packet)> incomingPacketChannel;

        /// <summary>
        /// Pipeline to convert incoming byte buffers to <see cref="NetworkPacket"/> instances.
        /// </summary>
        private readonly PacketPipeline<Memory<byte>, Memory<byte>, NetworkPacket> incomingPacketPipeline;

        /// <summary>
        /// Lock synchronisation object for the <see cref="logger"/> variable.
        /// </summary>
        private readonly object loggerLockObject = new object();

        private readonly Channel<(EndPoint destination, NetworkPacket packet)> outgoingPacketChannel;

        /// <summary>
        /// Pipeline to convert outgoing <see cref="NetworkPacket"/> instances to a byte buffer for sending.
        /// </summary>
        private readonly PacketPipeline<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPacketPipeline;

        private readonly Channel<(EndPoint origin, IRequestPacket request)> requestChannel;

        /// <summary>
        /// Cancellation token which allows observing the shutdown of the server. It is set when <see cref="ShutdownServer"/> is called.
        /// </summary>
        private readonly CancellationToken ServerShutdownToken;

        private readonly ConcurrentDictionary<EndPoint, Socket> streamConnections;

        /// <summary>
        /// A logger object allowing for writing debug messages to an output stream.
        /// </summary>
        private Logger logger;

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

            async Task StreamListenerWork(object clientArgsObj)
            {
                Socket clientSocket = (Socket)clientArgsObj;
                EndPoint clientEndPoint = clientSocket.RemoteEndPoint;

                logger.LogMessage($"Client handler started for {clientEndPoint}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    // TODO: implement receive buffer pooling
                    byte[] receiveBuffer = new byte[NetworkPacket.PacketSize];
                    Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

                    TransmissionResult result =
                        await DoReceiveFromAsync(clientSocket, clientEndPoint, SocketFlags.None, receiveBufferMemory, cancellationToken);

                    if (result.Count == 0)
                    {
                        break;
                    }

                    await incomingPacketChannel.Writer.WriteAsync((result.RemoteEndPoint, receiveBufferMemory),
                        cancellationToken);
                }

                logger.LogMessage($"Client handler stopped for {clientEndPoint}");

                await DoDisconnectAsync(clientSocket, cancellationToken);

                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close(1);
            }

            streamSocket.Listen(MaximumConnectionBacklog);

            while (!cancellationToken.IsCancellationRequested)
            {
                Socket clientSocket = await DoAcceptAsync(streamSocket, cancellationToken);

                if (!streamConnections.ContainsKey(clientSocket.RemoteEndPoint))
                {
                    streamConnections[clientSocket.RemoteEndPoint] = clientSocket;

                    await Task.Factory.StartNew(StreamListenerWork, streamConnections[clientSocket.RemoteEndPoint], ServerShutdownToken);
                }
                else
                {
                    logger.LogWarning($"Accepted duplicate connection from {clientSocket.RemoteEndPoint}");
                }
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
                    await DoReceiveFromAsync(datagramSocket, AnyRemoteEndPoint, SocketFlags.None,
                        receiveBufferMemory, cancellationToken);

                if (!datagramConnections.Contains(result.RemoteEndPoint))
                {
                    datagramConnections.Add(result.RemoteEndPoint);
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

                if (streamConnections.ContainsKey(destination))
                {
                    Socket streamConnection = streamConnections[destination];

                    await DoSendToAsync(streamConnection, destination, SocketFlags.None,
                        serialisedResponse, cancellationToken);
                }
                else if (datagramConnections.Contains(destination))
                {
                    await DoSendToAsync(datagramSocket, destination, SocketFlags.None,
                        serialisedResponse, cancellationToken);
                }
                else
                {
                    logger.LogWarning($"Packet destined for unknown destination: {destination}");
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

            sendToBufferPool = ArrayPool<byte>.Create(NetworkPacket.PacketSize, objectPoolSize);
            receiveFromBufferPool = ArrayPool<byte>.Create(NetworkPacket.PacketSize, objectPoolSize);

            clientSocketArgsPool =
                new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                    new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                        objectPoolSize));
            receiveArgsPool =
                new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                    new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                        objectPoolSize));
            sendArgsPool =
                new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                    new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                        objectPoolSize));

            for (int i = 0; i < objectPoolSize; i++)
            {
                SocketAsyncEventArgs clientArgs = new SocketAsyncEventArgs();
                clientArgs.Completed += HandleIOCompleted;
                clientSocketArgsPool.Return(clientArgs);

                SocketAsyncEventArgs receiveArgs = new SocketAsyncEventArgs();
                receiveArgs.Completed += HandleIOCompleted;
                receiveArgsPool.Return(receiveArgs);

                SocketAsyncEventArgs sendArgs = new SocketAsyncEventArgs();
                sendArgs.Completed += HandleIOCompleted;
                sendArgsPool.Return(sendArgs);
            }

            if (preallocateBuffers)
            {
                //TODO: Preallocate buffers someday
            }

            streamConnections = new ConcurrentDictionary<EndPoint, Socket>();
            datagramConnections = new HashSet<EndPoint>();

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

        /// <summary>
        /// Shuts down the connection, and releases managed and unmanaged resources.
        /// </summary>
        public void ShutdownServer()
        {
            logger.LogMessage("Signalling shutdown to all client connection handlers...");
            serverShutdownTokenSource.Cancel();
        }
    }
}