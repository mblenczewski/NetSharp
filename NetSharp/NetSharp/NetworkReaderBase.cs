using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetSharp
{
    public delegate bool NetworkRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer,
        Memory<byte> responseBuffer);

    public abstract class NetworkReaderBase<TState> : NetworkConnectionBase<TState> where TState : class
    {
        private readonly CancellationTokenSource shutdownTokenSource;

        protected readonly NetworkRequestHandler RequestHandler;
        protected readonly CancellationToken ShutdownToken;

        /// <inheritdoc />
        protected NetworkReaderBase(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
            maxPooledBuffersPerBucket, preallocatedStateObjects)
        {
            shutdownTokenSource = new CancellationTokenSource();
            ShutdownToken = shutdownTokenSource.Token;

            RequestHandler = requestHandler ?? DefaultRequestHandler;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

            shutdownTokenSource.Cancel();
            shutdownTokenSource.Dispose();

            base.Dispose(disposing);
        }

        public static bool DefaultRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer,
                    Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        public abstract void Start(ushort concurrentReadTasks);

        public void Stop()
        {
            shutdownTokenSource.Cancel();
        }
    }
}