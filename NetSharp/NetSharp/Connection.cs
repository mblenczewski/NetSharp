using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Pipelines;
using NetSharp.Sockets;
using NetSharp.Utils;

namespace NetSharp
{
    public class Connection : IDisposable
    {
        private readonly SocketAcceptor acceptor;
        private readonly ConcurrentDictionary<EndPoint, Connection> connections;
        private readonly CancellationTokenSource connectionShutdownTokenSource;
        private readonly PacketPipeline<Memory<byte>, Memory<byte>, NetworkPacket> incomingPacketPipeline;
        private readonly SocketReader listener;
        private readonly PacketPipeline<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPacketPipeline;
        private readonly Socket socket;
        private readonly SocketWriter transmitter;

        /// <summary>
        /// Destroys a <see cref="Connection"/> class instance, freeing all managed resources.
        /// </summary>
        ~Connection()
        {
            Dispose(false);
        }

        private void AcceptorWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            while (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void ListenerWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            while (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void PacketPipelineWork(object shutdownToken)
        {
            CancellationToken cancellationToken = (CancellationToken)shutdownToken;

            while (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        /// <summary>
        /// Lock synchronisation object for the <see cref="logger"/> variable.
        /// </summary>
        protected readonly object loggerLockObject = new object();

        /// <summary>
        /// Cancellation token which allows observing the shutdown of the server. It is set when <see cref="Shutdown()"/> is called.
        /// </summary>
        protected readonly CancellationToken ShutdownToken;

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
                socket.Dispose();
            }
        }

        internal Connection(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType,
            PacketPipeline<Memory<byte>, Memory<byte>, NetworkPacket> incomingPacketPipeline,
            PacketPipeline<NetworkPacket, Memory<byte>, Memory<byte>> outgoingPacketPipeline,
            int objectPoolSize = 10, bool preallocateBuffers = false, Stream? loggingStream = default,
            LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            connectionShutdownTokenSource = new CancellationTokenSource();
            ShutdownToken = connectionShutdownTokenSource.Token;

            socket = new Socket(addressFamily, socketType, protocolType);

            acceptor = new SocketAcceptor(objectPoolSize);
            listener = new SocketReader(NetworkPacket.PacketSize, objectPoolSize, preallocateBuffers);
            transmitter = new SocketWriter(NetworkPacket.PacketSize, objectPoolSize, preallocateBuffers);

            connections = new ConcurrentDictionary<EndPoint, Connection>();

            this.incomingPacketPipeline = incomingPacketPipeline;
            this.outgoingPacketPipeline = outgoingPacketPipeline;

            logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
        }

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
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return listener.ReceiveAsync(socket, flags, inputBuffer, cts.Token);
        }

        public Task<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return listener.ReceiveFromAsync(socket, remoteEndPoint, flags, inputBuffer, cts.Token);
        }

        /// <summary>
        /// Makes the connection listen for incoming client request packets, and handle them according to registered packet handler delegates.
        /// This work can be cancelled by calling <see cref="Shutdown()"/>.
        /// </summary>
        /// <returns>The task representing the connection work.</returns>
        public Task RunAsync()
        {
            Task acceptorThread =
                Task.Factory.StartNew(AcceptorWork, ShutdownToken, ShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            Task listenerThread =
                Task.Factory.StartNew(ListenerWork, ShutdownToken, ShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            Task packetPipelineThread =
                Task.Factory.StartNew(PacketPipelineWork, ShutdownToken, ShutdownToken, TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);

            return Task.WhenAll(acceptorThread, listenerThread, packetPipelineThread);
        }

        public Task<int> SendAsync(Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return transmitter.SendAsync(socket, flags, outputBuffer, cts.Token);
        }

        public Task<int> SendToAsync(EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return transmitter.SendToAsync(socket, remoteEndPoint, flags, outputBuffer, cts.Token);
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
        public void Shutdown()
        {
            connectionShutdownTokenSource.Cancel();
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
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            try
            {
                return await Task.Run(() =>
                {
                    socket.Bind(localEndPoint);

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