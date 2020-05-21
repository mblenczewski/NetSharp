using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw
{
    public abstract class RawNetworkWriterBase : RawNetworkConnectionBase, INetworkWriter
    {
        /// <inheritdoc />
        protected RawNetworkWriterBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        /// <inheritdoc />
        public abstract void Connect(EndPoint remoteEndPoint);

        /// <inheritdoc />
        public abstract ValueTask ConnectAsync(EndPoint remoteEndPoint);

        /// <inheritdoc />
        public abstract int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);

        protected readonly struct AsyncOperationToken
        {
            public readonly TaskCompletionSource<bool> CompletionSource;

            public AsyncOperationToken(TaskCompletionSource<bool> completionSource)
            {
                CompletionSource = completionSource;
            }
        }
    }
}