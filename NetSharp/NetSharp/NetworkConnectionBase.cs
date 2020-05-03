using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp
{
    public abstract class NetworkConnectionBase<TState> : IDisposable where TState : class
    {
        protected readonly ArrayPool<byte> BufferPool;
        protected readonly int BufferSize;
        protected readonly Socket Connection;
        protected readonly EndPoint DefaultEndPoint;
        protected readonly SlimObjectPool<TState> StateObjectPool;

        protected NetworkConnectionBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            Connection = rawConnection;

            BufferSize = maxPooledBufferSize;
            BufferPool = ArrayPool<byte>.Create(maxPooledBufferSize, maxPooledBuffersPerBucket);

            DefaultEndPoint = defaultEndPoint;

            StateObjectPool =
                new SlimObjectPool<TState>(CreateStateObject, ResetStateObject, DestroyStateObject, CanReuseStateObject);

            // TODO implement pooling in better way
            for (uint i = 0; i < preallocatedStateObjects; i++)
            {
                StateObjectPool.Return(CreateStateObject());
            }
        }

        protected abstract bool CanReuseStateObject(ref TState instance);

        protected abstract TState CreateStateObject();

        protected abstract void DestroyStateObject(TState instance);

        /// <summary>
        /// Allows for inheritors to dispose of their own resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            StateObjectPool.Dispose();
        }

        protected abstract void ResetStateObject(ref TState instance);

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}