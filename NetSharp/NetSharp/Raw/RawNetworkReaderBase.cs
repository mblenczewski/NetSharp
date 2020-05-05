using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetSharp.Raw
{
    public delegate bool NetworkRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        Memory<byte> responseBuffer);

    public abstract class RawNetworkReaderBase<TState> : RawNetworkConnectionBase<TState>, INetworkReader where TState : class
    {
        private readonly CancellationTokenSource shutdownTokenSource;

        protected readonly NetworkRequestHandler RequestHandler;
        protected readonly CancellationToken ShutdownToken;

        /// <inheritdoc />
        protected RawNetworkReaderBase(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler, int pooledPacketBufferSize,
            int pooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, pooledPacketBufferSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
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

        public static bool DefaultRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        /// <inheritdoc />
        public abstract void Start(ushort concurrentReadTasks);

        /// <inheritdoc />
        public void Stop()
        {
            shutdownTokenSource.Cancel();
        }
    }
}