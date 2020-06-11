using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetSharp.Raw
{
    public abstract class RawNetworkReaderBase : RawNetworkConnectionBase, INetworkReader
    {
        private readonly CancellationToken shutdownToken;
        private readonly CancellationTokenSource shutdownTokenSource;

        /// <inheritdoc />
        private protected RawNetworkReaderBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            shutdownTokenSource = new CancellationTokenSource();
            shutdownToken = shutdownTokenSource.Token;
        }

        protected ref readonly CancellationToken ShutdownToken => ref shutdownToken;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            shutdownTokenSource.Cancel();
            shutdownTokenSource.Dispose();

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        public abstract void Start(ushort concurrentReadTasks);

        /// <inheritdoc />
        public void Shutdown()
        {
            shutdownTokenSource.Cancel();
        }
    }
}