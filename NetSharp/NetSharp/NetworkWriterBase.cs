using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp
{
    public abstract class NetworkWriterBase<TState> : NetworkConnectionBase<TState> where TState : class
    {
        /// <inheritdoc />
        protected NetworkWriterBase(ref Socket rawConnection, int maxPooledBufferSize, int preallocatedStateObjects = 0)
            : base(ref rawConnection, maxPooledBufferSize, preallocatedStateObjects)
        {
        }

        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);

        public abstract ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);
    }
}