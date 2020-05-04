using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw
{
    public abstract class NetworkWriterBase<TState> : NetworkConnectionBase<TState> where TState : class
    {
        /// <inheritdoc />
        protected NetworkWriterBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, maxPooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        public abstract void Connect(EndPoint remoteEndPoint);

        public abstract ValueTask ConnectAsync(EndPoint remoteEndPoint);

        public abstract int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        public abstract ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);

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