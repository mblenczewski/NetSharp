using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp
{
    public abstract class NetworkConnectionBase<TState> : IDisposable where TState : class
    {
        private readonly ArrayPool<byte> bufferPool;
        protected readonly SlimObjectPool<TState> StateObjectPool;
        protected readonly int BufferSize;
        protected readonly Socket Connection;

        protected NetworkConnectionBase(ref Socket rawConnection, int maxPooledBufferSize, int preallocatedStateObjects = 0)
        {
            Connection = rawConnection;

            BufferSize = maxPooledBufferSize;
            bufferPool = ArrayPool<byte>.Create(maxPooledBufferSize, 1_000);

            StateObjectPool =
                new SlimObjectPool<TState>(CreateStateObject, ResetStateObject, DestroyStateObject, CanReuseStateObject);
        }

        protected abstract bool CanReuseStateObject(in TState instance);

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

        protected RentedBufferHandle RentBuffer(int desiredBufferSize)
        {
            byte[] rentedBuffer = bufferPool.Rent(desiredBufferSize);

            return new RentedBufferHandle(ref rentedBuffer);
        }

        protected abstract void ResetStateObject(ref TState instance);

        protected void ReturnBuffer(RentedBufferHandle handle)
        {
            bufferPool.Return(handle.RentedBuffer, true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected readonly struct RentedBufferHandle
        {
            public readonly byte[] RentedBuffer;

            internal RentedBufferHandle(ref byte[] rentedBuffer)
            {
                RentedBuffer = rentedBuffer;
            }
        }
    }
}