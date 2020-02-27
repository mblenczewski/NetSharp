using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Sockets;
using NetSharp.Utils;

namespace NetSharp
{
    public class Connection : IDisposable
    {
        private readonly SocketAcceptor acceptor;
        private readonly ConcurrentDictionary<EndPoint, Connection> connections;
        private readonly CancellationTokenSource connectionShutdownTokenSource;
        private readonly SocketReader listener;
        private readonly Socket socket;
        private readonly SocketWriter transmitter;

        /// <summary>
        /// Destroys a <see cref="Connection"/> class instance, freeing all managed resources.
        /// </summary>
        ~Connection()
        {
            Dispose(false);
        }

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

            logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Task<TransmissionResult> ReceiveAsync(Memory<byte> inputBuffer, SocketFlags flags)
            => ReceiveAsync(inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public Task<TransmissionResult> ReceiveAsync(Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return listener.ReceiveAsync(socket, flags, inputBuffer, cts.Token);
        }

        public Task<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags)
                    => ReceiveFromAsync(remoteEndPoint, inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public Task<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return listener.ReceiveFromAsync(socket, remoteEndPoint, flags, inputBuffer, cts.Token);
        }

        public Task<int> SendAsync(Memory<byte> outputBuffer, SocketFlags flags)
                    => SendAsync(outputBuffer, flags, Timeout.InfiniteTimeSpan);

        public Task<int> SendAsync(Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return transmitter.SendAsync(socket, flags, outputBuffer, cts.Token);
        }

        public Task<int> SendToAsync(EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags)
                    => SendToAsync(remoteEndPoint, outputBuffer, flags, Timeout.InfiniteTimeSpan);

        public Task<int> SendToAsync(EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ShutdownToken);

            return transmitter.SendToAsync(socket, remoteEndPoint, flags, outputBuffer, cts.Token);
        }

        public void SetLoggingStream(Stream? loggingStream, LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
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