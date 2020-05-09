using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw
{
    public abstract class RawNetworkConnectionBase : IDisposable
    {
        // https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs
        private const int DefaultMaxPooledBufferSize = 1024 * 1024, DefaultMaxPooledBuffersPerBucket = 50;

        protected readonly SlimObjectPool<SocketAsyncEventArgs> ArgsPool;
        protected readonly ArrayPool<byte> BufferPool;
        protected readonly Socket Connection;
        protected readonly EndPoint DefaultEndPoint;

        protected RawNetworkConnectionBase(ref Socket rawConnection, EndPoint defaultEndPoint, int pooledPacketBufferSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0)
        {
            Connection = rawConnection;

            BufferPool = pooledPacketBufferSize <= DefaultMaxPooledBufferSize && pooledBuffersPerBucket <= DefaultMaxPooledBuffersPerBucket
                ? ArrayPool<byte>.Shared
                : BufferPool = ArrayPool<byte>.Create(pooledPacketBufferSize, pooledBuffersPerBucket);

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