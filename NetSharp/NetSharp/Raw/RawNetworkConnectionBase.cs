using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw
{
    public abstract class RawNetworkConnectionBase : IDisposable
    {
        protected readonly SlimObjectPool<SocketAsyncEventArgs> ArgsPool;
        protected readonly ArrayPool<byte> BufferPool;
        protected readonly Socket Connection;
        protected readonly EndPoint DefaultEndPoint;
        protected readonly int PacketBufferSize;

        protected RawNetworkConnectionBase(ref Socket rawConnection, EndPoint defaultEndPoint, int pooledPacketBufferSize,
            int pooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            Connection = rawConnection;

            PacketBufferSize = pooledPacketBufferSize;
            BufferPool = ArrayPool<byte>.Create(pooledPacketBufferSize, pooledBuffersPerBucket);

            DefaultEndPoint = defaultEndPoint;

            ArgsPool =
                new SlimObjectPool<SocketAsyncEventArgs>(CreateStateObject, ResetStateObject, DestroyStateObject, CanReuseStateObject);

            // TODO implement pooling in better way
            for (uint i = 0; i < preallocatedStateObjects; i++)
            {
                ArgsPool.Return(CreateStateObject());
            }
        }

        protected abstract bool CanReuseStateObject(ref SocketAsyncEventArgs instance);

        protected abstract SocketAsyncEventArgs CreateStateObject();

        protected abstract void DestroyStateObject(SocketAsyncEventArgs instance);

        /// <summary>
        /// Allows for inheritors to dispose of their own resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            ArgsPool.Dispose();
        }

        protected abstract void ResetStateObject(ref SocketAsyncEventArgs instance);

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}