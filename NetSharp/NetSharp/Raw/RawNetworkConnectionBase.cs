using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using NetSharp.Utils;

namespace NetSharp.Raw
{
    public abstract class RawNetworkConnectionBase : IDisposable
    {
        private readonly SlimObjectPool<SocketAsyncEventArgs> argsPool;

        private readonly ArrayPool<byte> bufferPool;

        private readonly Socket connection;

        private readonly EndPoint defaultEndPoint;

        // https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs
        protected const int DefaultMaxPooledBufferSize = 1024 * 1024, DefaultMaxPooledBuffersPerBucket = 50;

        protected RawNetworkConnectionBase(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0)
        {
            connection = rawConnection;

            bufferPool = maxPooledBufferSize <= DefaultMaxPooledBufferSize && pooledBuffersPerBucket <= DefaultMaxPooledBuffersPerBucket
                ? ArrayPool<byte>.Shared
                : ArrayPool<byte>.Create(maxPooledBufferSize, pooledBuffersPerBucket);

            this.defaultEndPoint = defaultEndPoint;

            argsPool = new SlimObjectPool<SocketAsyncEventArgs>(CreateStateObject, ResetStateObject, DestroyStateObject, CanReuseStateObject);

            // TODO implement pooling in better way
            for (uint i = 0; i < preallocatedStateObjects; i++)
            {
                argsPool.Return(CreateStateObject());
            }
        }

        protected ref readonly SlimObjectPool<SocketAsyncEventArgs> ArgsPool => ref argsPool;

        protected ref readonly ArrayPool<byte> BufferPool => ref bufferPool;

        protected ref readonly Socket Connection => ref connection;

        protected ref readonly EndPoint DefaultEndPoint => ref defaultEndPoint;

        protected abstract bool CanReuseStateObject(ref SocketAsyncEventArgs instance);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CleanupTransmissionBufferAndState(SocketAsyncEventArgs args)
        {
            if (args != default)
            {
                bufferPool.Return(args.Buffer, true);
                argsPool.Return(args);
            }
        }

        protected abstract SocketAsyncEventArgs CreateStateObject();

        protected abstract void DestroyStateObject(SocketAsyncEventArgs instance);

        /// <summary>
        /// Allows for inheritors to dispose of their own resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            argsPool.Dispose();
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