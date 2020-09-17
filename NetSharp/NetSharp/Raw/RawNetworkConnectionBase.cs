using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

using NetSharp.Utils;

namespace NetSharp.Raw
{
    /// <summary>
    /// Defines fields and methods common to all network connections.
    /// </summary>
    public abstract class RawNetworkConnectionBase : IDisposable
    {
        /// <summary>
        /// The maximum size of a pooled buffer that can be used with the <see cref="ArrayPool{T}.Shared" /> property, before a new custom pool must
        /// be created. Taken from: https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs.
        /// </summary>
        protected const int DefaultMaxPooledBufferSize = 1024 * 1024;

        /// <summary>
        /// The maximum number of pooled buffers per bucket that can be used with the <see cref="ArrayPool{T}.Shared" /> property, before a new custom
        /// pool must be created. Taken from: https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/shared/System/Buffers/ConfigurableArrayPool.cs.
        /// </summary>
        protected const int DefaultMaxPooledBuffersPerBucket = 50;

        /// <summary>
        /// The maximum size that a user supplied data buffer can be to fit into a UDP datagram.
        /// </summary>
        protected const int MaxDatagramSize = ushort.MaxValue - 28; // 65535 - 28 = 65507

        protected volatile bool ConnectionDisposed;

        private readonly SlimObjectPool<SocketAsyncEventArgs> argsPool;

        private readonly ArrayPool<byte> bufferPool;

        private readonly Socket connection;

        private readonly EndPoint defaultEndPoint;

        /// <summary>
        /// Initialises a new instance of the <see cref="RawNetworkConnectionBase" /> class.
        /// </summary>
        /// <param name="rawConnection">
        /// The underlying <see cref="Socket" /> to use for the connection.
        /// </param>
        /// <param name="defaultEndPoint">
        /// The default endpoint to use to represent remote clients.
        /// </param>
        /// <param name="maxPooledBufferSize">
        /// The maximum size of a pooled buffer.
        /// </param>
        /// <param name="pooledBuffersPerBucket">
        /// The number of pooled buffers to hold in a single pool bucket.
        /// </param>
        /// <param name="preallocatedStateObjects">
        /// The number of state objects to preallocate.
        /// </param>
        protected RawNetworkConnectionBase(
            ref Socket rawConnection,
            EndPoint defaultEndPoint,
            int maxPooledBufferSize,
            int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0)
        {
            connection = rawConnection;

            bufferPool = maxPooledBufferSize <= DefaultMaxPooledBufferSize && pooledBuffersPerBucket <= DefaultMaxPooledBuffersPerBucket
                ? ArrayPool<byte>.Shared
                : ArrayPool<byte>.Create(maxPooledBufferSize, pooledBuffersPerBucket);

            this.defaultEndPoint = defaultEndPoint;

            argsPool = new SlimObjectPool<SocketAsyncEventArgs>(CreateStateObject, ResetStateObject, DestroyStateObject, CanReuseStateObject);

            // TODO implement pooling in better way
            /*
            for (uint i = 0; i < preallocatedStateObjects; i++)
            {
                argsPool.Return(CreateStateObject());
            }
            */
        }

        /// <summary>
        /// The object pool to use to pool <see cref="SocketAsyncEventArgs" /> instances.
        /// </summary>
        protected SlimObjectPool<SocketAsyncEventArgs> ArgsPool => argsPool;

        /// <summary>
        /// The object pool to use to pool byte buffer instance.
        /// </summary>
        protected ArrayPool<byte> BufferPool => bufferPool;

        /// <summary>
        /// The underlying connection socket.
        /// </summary>
        protected Socket Connection => connection;

        /// <summary>
        /// The default endpoint to use to represent remote clients.
        /// </summary>
        protected EndPoint DefaultEndPoint => defaultEndPoint;

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc cref="SlimObjectPool{T}.CanReuseObjectPredicate" />
        protected abstract bool CanReuseStateObject(ref SocketAsyncEventArgs instance);

        /// <summary>
        /// Performs cleanup on the given <paramref name="args" /> instance. Neither the given <paramref name="args"/> instance, nor the
        /// <see cref="SocketAsyncEventArgs.Buffer" /> or <see cref="SocketAsyncEventArgs.MemoryBuffer" /> attached to it can be used after this call.
        /// </summary>
        /// <param name="args">
        /// The used <see cref="SocketAsyncEventArgs" /> that can be cleaned up to be reused.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void CleanupTransmissionBufferAndState(SocketAsyncEventArgs args)
        {
            Debug.Assert(args != default, "Attempted to cleanup \'null\' SocketAsyncEventArgs instance!");

            if (args.Buffer != default)
            {
                bufferPool.Return(args.Buffer, true);
                args.SetBuffer(Array.Empty<byte>(), 0, 0);
            }

            argsPool.Return(args);
        }

        /// <inheritdoc cref="SlimObjectPool{T}.CreateObjectDelegate" />
        protected abstract SocketAsyncEventArgs CreateStateObject();

        /// <inheritdoc cref="SlimObjectPool{T}.DestroyObjectDelegate" />
        protected abstract void DestroyStateObject(SocketAsyncEventArgs instance);

        /// <summary>
        /// Allows for inheritors to dispose of their own resources.
        /// </summary>
        /// <param name="disposing">
        /// Whether the <see cref="Dispose()" /> method is being called.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            ConnectionDisposed = true;
            Debug.WriteLine("Disposing of NetworkConnectionBase!");

            if (disposing)
            {
                // dispose of managed resources
                argsPool?.Dispose();
            }

            // dispose of unmanaged resources
        }

        /// <inheritdoc cref="SlimObjectPool{T}.ResetObjectDelegate" />
        protected abstract void ResetStateObject(ref SocketAsyncEventArgs instance);
    }
}
