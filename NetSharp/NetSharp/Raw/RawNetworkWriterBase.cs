using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Interfaces;

namespace NetSharp.Raw
{
    /// <summary>
    /// Provides fields and methods common to all network writer connections.
    /// </summary>
    public abstract class RawNetworkWriterBase : RawNetworkConnectionBase, IRawNetworkWriter
    {
        /// <inheritdoc cref="RawNetworkConnectionBase(ref Socket, EndPoint, int, int, uint)"/>
        protected RawNetworkWriterBase(
            ref Socket rawConnection,
            EndPoint defaultEndPoint,
            int maxPooledBufferSize = DefaultMaxPooledBufferSize,
            int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0)
            : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        /// <inheritdoc />
        public abstract int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None);
    }
}
