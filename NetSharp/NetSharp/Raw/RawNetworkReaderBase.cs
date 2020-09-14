using System.Net;
using System.Net.Sockets;
using System.Threading;

using NetSharp.Interfaces;

namespace NetSharp.Raw
{
    /// <summary>
    /// Provides fields and methods common to all network reader connections.
    /// </summary>
    public abstract class RawNetworkReaderBase : RawNetworkConnectionBase, IRawNetworkReader
    {
        private readonly CancellationToken shutdownToken;

        private readonly CancellationTokenSource shutdownTokenSource;

        /// <inheritdoc cref="RawNetworkConnectionBase(ref Socket, EndPoint, int, int, uint)"/>
        private protected RawNetworkReaderBase(
            ref Socket rawConnection,
            EndPoint defaultEndPoint,
            int maxPooledBufferSize,
            int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0)
            : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            shutdownTokenSource = new CancellationTokenSource();
            shutdownToken = shutdownTokenSource.Token;
        }

        /// <summary>
        /// The <see cref="CancellationToken" /> for the network reader.
        /// </summary>
        protected ref readonly CancellationToken ShutdownToken => ref shutdownToken;

        /// <inheritdoc />
        public void Shutdown()
        {
            shutdownTokenSource.Cancel();
        }

        /// <inheritdoc />
        public abstract void Start(ushort concurrentReadTasks);

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
    }
}
