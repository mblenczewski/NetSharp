using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp
{
    public abstract class NetworkWriterBase<TState> : NetworkConnectionBase<TState> where TState : class
    {
        /// <inheritdoc />
        protected NetworkWriterBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, maxPooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        public abstract int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        public abstract ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
                            SocketFlags flags = SocketFlags.None);

        public abstract ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);
    }
}