using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp
{
    public delegate bool NetworkRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer,
        Memory<byte> responseBuffer);

    public abstract class NetworkReaderBase<TState> : NetworkConnectionBase<TState> where TState : class
    {
        private readonly CancellationTokenSource shutdownTokenSource;

        protected readonly CancellationToken ShutdownToken;

        protected readonly NetworkRequestHandler RequestHandler;

        protected readonly EndPoint DefaultEndPoint;

        /// <inheritdoc />
        protected NetworkReaderBase(ref Socket rawConnection, NetworkRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxPooledBufferSize, int preallocatedStateObjects = 0)
            : base(ref rawConnection, maxPooledBufferSize, preallocatedStateObjects)
        {
            shutdownTokenSource = new CancellationTokenSource();
            ShutdownToken = shutdownTokenSource.Token;

            DefaultEndPoint = defaultEndPoint;

            RequestHandler = requestHandler ?? DefaultRequestHandler;
        }

        public static bool DefaultRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer,
            Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;

            shutdownTokenSource.Cancel();
            shutdownTokenSource.Dispose();

            base.Dispose(disposing);
        }

        public abstract void Start(ushort concurrentReadTasks);

        public void Stop()
        {
            shutdownTokenSource.Cancel();
        }
    }
}