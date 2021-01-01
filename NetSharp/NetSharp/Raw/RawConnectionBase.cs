using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using NetSharp.Utils;

namespace NetSharp.Raw
{
    /// <summary>
    /// Provides base functionality to all raw network connections.
    /// </summary>
    public abstract class RawConnectionBase : IDisposable
    {
        private readonly ArrayPool<byte> bufferPool;
        private readonly Socket connection;
        private readonly SlimObjectPool<SocketAsyncEventArgs> socketArgsPool;

        private int activeOperations;
        private readonly object activeOperationsLock = new object();
        private bool isBound;
        private bool isDisposed;

        /// <summary>
        /// Initialises a new instance of the <see cref="RawConnectionBase" /> class.
        /// </summary>
        /// <param name="connectionSocketType">
        /// The socket type for the underlying network connection.
        /// </param>
        /// <param name="connectionProtocolType">
        /// The protocol type for the underlying network connection.
        /// </param>
        /// <param name="defaultRemoteEndPoint">
        /// The default remote endpoint to which network writes will be made.
        /// </param>
        protected RawConnectionBase(
            SocketType connectionSocketType,
            ProtocolType connectionProtocolType,
            EndPoint defaultRemoteEndPoint)
        {
            bufferPool = ArrayPool<byte>.Shared;

            connection = new Socket(connectionSocketType, connectionProtocolType);

            socketArgsPool =
                new SlimObjectPool<SocketAsyncEventArgs>(CreateSocketArgs, ResetSocketArgs, DestroySocketArgs);

            DefaultRemoteEndPoint = defaultRemoteEndPoint;
        }

        /// <summary>
        /// The default remote endpoint to which network writes are made.
        /// </summary>
        public EndPoint DefaultRemoteEndPoint { get; }

        /// <summary>
        /// The currently bound local endpoint.
        /// </summary>
        public EndPoint LocalEndPoint => connection.LocalEndPoint;

        /// <summary>
        /// The currently connected remote endpoint.
        /// </summary>
        public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

        /// <summary>
        /// The underlying network connection.
        /// </summary>
        protected ref readonly Socket Connection => ref connection;

        /// <summary>
        /// Whether the underlying network connection has been disposed or not.
        /// </summary>
        protected bool IsDisposed => isDisposed;

        /// <summary>
        /// Binds the underlying network connection to the given local endpoint.
        /// </summary>
        /// <param name="localEndPoint">
        /// The local network endpoint to which we should bind.
        /// </param>
        public void Bind(EndPoint localEndPoint)
        {
            connection.Bind(localEndPoint);
            isBound = true;
        }

        /// <summary>
        /// Stops listening for incoming connections and data, and releases all managed resources.
        /// </summary>
        public void Close()
        {
            // TODO: consider doing other stuff to close the connection?
            Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Starts listening to the network for incoming connections and data.
        /// </summary>
        /// <param name="concurrentTasks">
        /// The number of concurrent read tasks that should be maintained.
        /// </param>
        public void Start(int concurrentTasks = 1)
        {
            Debug.Assert(isBound, "Connection must be bound before we can start listening to the network!");

            StartHook(concurrentTasks);

            int taskCount = concurrentTasks < 1 ? 1 : concurrentTasks;

            for (int i = 0; i < taskCount; i++)
            {
                HandlerTaskWork();
            }
        }

        /// <summary>
        /// Allows for custom initialisation of a pooled <see cref="SocketAsyncEventArgs" /> instance.
        /// </summary>
        /// <param name="instance">
        /// The instance to initialise.
        /// </param>
        protected virtual void CreateSocketArgsHook(ref SocketAsyncEventArgs instance)
        {
        }

        /// <summary>
        /// Allows for custom destruction of a pooled <see cref="SocketAsyncEventArgs" /> instance.
        /// </summary>
        /// <param name="instance">
        /// The instance to destroy.
        /// </param>
        protected virtual void DestroySocketArgsHook(ref SocketAsyncEventArgs instance)
        {
        }

        /// <summary>
        /// Disposes of this <see cref="RawConnectionBase" /> instance.
        /// </summary>
        /// <param name="disposing">
        /// Whether the <see cref="Dispose()" /> method was called.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            if (disposing)
            {
                connection.Dispose();

                lock (activeOperationsLock)
                {
                    while (activeOperations > 0)
                    {
                        // will keep reacquiring the lock and blocking until we reach the activeOperations == 0 case
                        _ = Monitor.Wait(activeOperationsLock);
                    }
                }

                socketArgsPool.Dispose();
            }

            // TODO: Set large fields to null
            isDisposed = true;
        }

        /// <summary>
        /// Handler work delegate, started when a call to <see cref="Start" /> is made.
        /// </summary>
        protected abstract void HandlerTaskWork();

        /// <summary>
        /// Rents a pooled buffer of at least the specified length. The buffer MUST be returned via a call to <see
        /// cref="ReturnBuffer(byte[], bool)" /> once it has been used.
        /// </summary>
        /// <param name="minimumBufferLength">
        /// The minimum length of the rented buffer.
        /// </param>
        /// <returns>
        /// The rented buffer.
        /// </returns>
        protected byte[] RentBuffer(int minimumBufferLength)
        {
            return bufferPool.Rent(minimumBufferLength);
        }

        /// <summary>
        /// Rents a pooled <see cref="SocketAsyncEventArgs" /> instance. The socket args MUST be returned via a call to
        /// <see cref="ReturnSocketArgs(SocketAsyncEventArgs)" /> once they have been used.
        /// </summary>
        /// <returns>
        /// The rented socket args.
        /// </returns>
        protected SocketAsyncEventArgs RentSocketArgs()
        {
            _ = Interlocked.Increment(ref activeOperations);

            return socketArgsPool.Rent();
        }

        /// <summary>
        /// Allows for custom resetting of a pooled <see cref="SocketAsyncEventArgs" /> instance.
        /// </summary>
        /// <param name="instance">
        /// The instance to reset.
        /// </param>
        protected virtual void ResetSocketArgsHook(ref SocketAsyncEventArgs instance)
        {
        }

        /// <summary>
        /// Returns a previously rented pooled buffer, optionally without clearing it.
        /// </summary>
        /// <param name="buffer">
        /// The rented buffer.
        /// </param>
        /// <param name="clearBuffer">
        /// Whether to clear the data held in the buffer.
        /// </param>
        protected void ReturnBuffer(byte[] buffer, bool clearBuffer = true)
        {
            bufferPool.Return(buffer, clearBuffer);
        }

        /// <summary>
        /// Returns a previously rented <see cref="SocketAsyncEventArgs" /> instance.
        /// </summary>
        /// <param name="socketArgs">
        /// The rented socket args.
        /// </param>
        protected void ReturnSocketArgs(SocketAsyncEventArgs socketArgs)
        {
            socketArgsPool.Return(socketArgs);

            lock (activeOperationsLock)
            {
                _ = Interlocked.Decrement(ref activeOperations);
                Monitor.Pulse(activeOperationsLock); // signals that we might have reached the activeOperations == 0 state
            }
        }

        /// <summary>
        /// Allows for custom setup before we start listening to the network.
        /// </summary>
        /// <param name="concurrentTasks">
        /// The number of concurrent read tasks that should be maintained.
        /// </param>
        protected virtual void StartHook(int concurrentTasks)
        {
        }

        private SocketAsyncEventArgs CreateSocketArgs()
        {
            SocketAsyncEventArgs instance = new SocketAsyncEventArgs { RemoteEndPoint = DefaultRemoteEndPoint };

            CreateSocketArgsHook(ref instance);

            return instance;
        }

        private void DestroySocketArgs(SocketAsyncEventArgs instance)
        {
            DestroySocketArgsHook(ref instance);

            instance.Dispose();
        }

        private void ResetSocketArgs(ref SocketAsyncEventArgs instance)
        {
            ResetSocketArgsHook(ref instance);

            instance.RemoteEndPoint = DefaultRemoteEndPoint;
        }
    }
}
