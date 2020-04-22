using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Abstract base class for clients and servers.
    /// </summary>
    /// TODO add access to socket options
    public abstract class SocketConnection : IDisposable
    {
        /// <summary>
        /// The underlying <see cref="Socket"/> which provides access to network operations.
        /// </summary>
        protected Socket Connection;

        /// <summary>
        /// Pools arrays to function as temporary buffers during network read/write operations.
        /// </summary>
        protected readonly ArrayPool<byte> BufferPool;

        /// <summary>
        /// Pools <see cref="SocketAsyncEventArgs"/> objects for use during network read/write operations and calls
        /// to <see cref="Socket"/>.XXXAsync(<see cref="SocketAsyncEventArgs"/>) methods.
        /// </summary>
        protected readonly SlimObjectPool<SocketAsyncEventArgs> TransmissionArgsPool;

        /// <summary>
        /// Constructs a new instance of the <see cref="SocketConnection"/> class.
        /// </summary>
        /// <param name="connectionAddressFamily">The address family for the underlying socket.</param>
        /// <param name="connectionSocketType">The socket type for the underlying socket.</param>
        /// <param name="connectionProtocolType">The protocol type for the underlying socket.</param>
        /// <param name="maxPooledBufferLength">The maximum size of the buffers stored in the <see cref="BufferPool"/>.</param>
        /// <param name="preallocatedTransmissionArgs"> The number of <see cref="SocketAsyncEventArgs"/> objects to initially preallocate.</param>
        protected internal SocketConnection(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType,
            in ProtocolType connectionProtocolType, in int maxPooledBufferLength, in ushort preallocatedTransmissionArgs)
        {
            Connection = new Socket(connectionAddressFamily, connectionSocketType, connectionProtocolType);

            BufferPool = ArrayPool<byte>.Create(maxPooledBufferLength, 1000);

            TransmissionArgsPool = new SlimObjectPool<SocketAsyncEventArgs>(CreateTransmissionArgs,
                ResetTransmissionArgs, DestroyTransmissionArgs, CanTransmissionArgsBeReused);

            for (ushort i = 0; i < preallocatedTransmissionArgs; i++)
            {
                SocketAsyncEventArgs args = CreateTransmissionArgs();

                TransmissionArgsPool.Return(args);
            }
        }

        /// <summary>
        /// Delegate method used to construct fresh <see cref="SocketAsyncEventArgs"/> instances for use in the
        /// <see cref="TransmissionArgsPool"/>. The resulting instance should register <see cref="HandleIoCompleted"/>
        /// as an event handler for the <see cref="SocketAsyncEventArgs.Completed"/> event.
        /// </summary>
        /// <returns>The configured <see cref="SocketAsyncEventArgs"/> instance.</returns>
        protected abstract SocketAsyncEventArgs CreateTransmissionArgs();

        /// <summary>
        /// Delegate method used to reset used <see cref="SocketAsyncEventArgs"/> instances for later reuse by
        /// the <see cref="TransmissionArgsPool"/>.
        /// </summary>
        /// <param name="args">The <see cref="SocketAsyncEventArgs"/> instance that should be reset.</param>
        protected abstract void ResetTransmissionArgs(SocketAsyncEventArgs args);

        /// <summary>
        /// Delegate method used to check whether the given used <see cref="SocketAsyncEventArgs"/> instance can be reused
        /// by the <see cref="TransmissionArgsPool"/>. If this method returns <c>true</c>, <see cref="ResetTransmissionArgs"/>
        /// is called on the given <paramref name="args"/>. Otherwise, <see cref="DestroyTransmissionArgs"/> is called.
        /// </summary>
        /// <param name="args">The <see cref="SocketAsyncEventArgs"/> instance to check.</param>
        /// <returns>Whether the given <paramref name="args"/> should be reset and reused, or should be destroyed.</returns>
        protected abstract bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args);

        /// <summary>
        /// Delegate method to destroy used <see cref="SocketAsyncEventArgs"/> instances that cannot be reused by the
        /// <see cref="TransmissionArgsPool"/>. This method should deregister <see cref="HandleIoCompleted"/> as an
        /// event handler for the <see cref="SocketAsyncEventArgs.Completed"/> event.
        /// </summary>
        /// <param name="remoteConnectionArgs">The <see cref="SocketAsyncEventArgs"/> which should be destroyed.</param>
        protected abstract void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs);

        /// <summary>
        /// Delegate method to handle asynchronous network IO completion via the <see cref="SocketAsyncEventArgs.Completed"/> event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="args">The <see cref="SocketAsyncEventArgs"/> instance associated with the asynchronous network IO.</param>
        protected abstract void HandleIoCompleted(object sender, SocketAsyncEventArgs args);

        /// <summary>
        /// Binds the underlying socket.
        /// </summary>
        /// <param name="localEndPoint">The end point to which the socket should be bound.</param>
        public void Bind(in EndPoint localEndPoint)
        {
            Connection.Bind(localEndPoint);
        }

        /// <summary>
        /// Shuts down the underlying socket.
        /// </summary>
        /// <param name="how">Which socket transmission functions should be shut down on the socket.</param>
        public void Shutdown(SocketShutdown how)
        {
            try
            {
                Connection.Shutdown(how);
            }
            catch (SocketException) { }
        }

        /// <summary>
        /// Disposes of managed and unmanaged resources used by the <see cref="SocketConnection"/> class.
        /// </summary>
        /// <param name="disposing">Whether this call was made by a call to <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Connection.Close();
            Connection.Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}