using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Abstract base class for clients.
    /// </summary>
    /// TODO implement proper memory leak-free cancellation of network IO operations
    public abstract class SocketClient : SocketConnection
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="SocketClient" /> class.
        /// </summary>
        /// <param name="connectionAddressFamily">
        /// The address family that the underlying connection should use.
        /// </param>
        /// <param name="connectionSocketType">
        /// The socket type that the underlying connection should use.
        /// </param>
        /// <param name="connectionProtocolType">
        /// The protocol type that the underlying connection should use.
        /// </param>
        /// <param name="pooledBufferMaxSize">
        /// The maximum size in bytes of buffers held in the buffer pool.
        /// </param>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of transmission args to preallocate.
        /// </param>
        protected SocketClient(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType,
            in int pooledBufferMaxSize, in ushort preallocatedTransmissionArgs) : base(in connectionAddressFamily, in connectionSocketType,
            in connectionProtocolType, pooledBufferMaxSize, preallocatedTransmissionArgs)
        {
        }

        /// <summary>
        /// Callback for the cancellation of an asynchronous network operation.
        /// </summary>
        /// <param name="state">
        /// The <see cref="SocketAsyncEventArgs" /> state object for the operation.
        /// </param>
        protected void CancelAsyncOperationCallback(object state)
        {
            SocketAsyncEventArgs args = (SocketAsyncEventArgs)state;

            AsyncOperationToken token = (AsyncOperationToken)args.UserToken;

            token.CompletionSource.SetResult(false);

            DestroyTransmissionArgs(args);
        }

        /// <summary>
        /// Callback for the cancellation of an asynchronous network receive operation.
        /// </summary>
        /// <param name="state">
        /// The <see cref="SocketAsyncEventArgs" /> state object for the operation.
        /// </param>
        protected void CancelAsyncReceiveCallback(object state)
        {
            SocketAsyncEventArgs args = (SocketAsyncEventArgs)state;

            AsyncReceiveToken token = (AsyncReceiveToken)args.UserToken;

            token.CompletionSource.SetResult(TransmissionResult.Timeout);

            DestroyTransmissionArgs(args);
        }

        /// <summary>
        /// Callback for the cancellation of an asynchronous network send operation.
        /// </summary>
        /// <param name="state">
        /// The <see cref="SocketAsyncEventArgs" /> state object for the operation.
        /// </param>
        protected void CancelAsyncSendCallback(object state)
        {
            SocketAsyncEventArgs args = (SocketAsyncEventArgs)state;

            AsyncSendToken token = (AsyncSendToken)args.UserToken;

            token.CompletionSource.SetResult(TransmissionResult.Timeout);

            BufferPool.Return(token.RentedBuffer, true);
            DestroyTransmissionArgs(args);
        }

        /// <summary>
        /// Connects the client to the specified end point. If called on a <see cref="SocketType.Dgram" />-based client, this method configures the
        /// default remote host, and the client will ignore any packets not coming from this default host (i.e the given <paramref name="remoteEndPoint" />).
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote end point which to which to connect the client.
        /// </param>
        public void Connect(in EndPoint remoteEndPoint)
        {
            Connection.Connect(remoteEndPoint);
        }

        /// <summary>
        /// Asynchronously connects the client to the specified end point. If called on a <see cref="SocketType.Dgram" />-based client, this method
        /// configures the default remote host, and the client will ignore any packets not coming from this default host (i.e the given <paramref name="remoteEndPoint" />).
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote end point which to which to connect the client.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to observe during the asynchronous operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask" /> representing the connection attempt.
        /// </returns>
        public ValueTask ConnectAsync(in EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncOperationToken(in tcs, in cancellationToken);

            if (cancellationToken == default)
            {
                if (Connection.ConnectAsync(args)) return new ValueTask(tcs.Task);
            }
            else
            {
                // TODO find out why the fricc we leak memory
                CancellationTokenRegistration cancellationRegistration =
                    cancellationToken.Register(CancelAsyncOperationCallback, args);

                if (Connection.ConnectAsync(args))
                    return new ValueTask(
                        tcs.Task.ContinueWith((task, state) =>
                        {
                            ((CancellationTokenRegistration)state).Dispose();

                            return task.Result;
                        }, cancellationRegistration, CancellationToken.None)
                    );

                cancellationRegistration.Dispose();
            }

            TransmissionArgsPool.Return(args);

            return new ValueTask();
        }

        /// <summary>
        /// Listens for data from the specified endpoint, placing the data in the given buffer. On connection-oriented protocols, the given endpoint
        /// is ignored in favour of the default remote host set up by a call to <see cref="Connect" /> or <see cref="ConnectAsync" />.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint from which data should be received. Ignored on connection-oriented protocols.
        /// </param>
        /// <param name="receiveBuffer">
        /// The buffer into which data is to be received.
        /// </param>
        /// <param name="flags">
        /// The socket flags associated with the send operation.
        /// </param>
        /// <returns>
        /// The result of the receive operation.
        /// </returns>
        public abstract TransmissionResult Receive(in EndPoint remoteEndPoint, byte[] receiveBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Asynchronously listens for data from the specified endpoint, placing the data in the given buffer. On connection-oriented protocols, the
        /// given endpoint is ignored in favour of the default remote host set up by a call to <see cref="Connect" /> or <see cref="ConnectAsync" />.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint from which data should be received. Ignored on connection-oriented protocols.
        /// </param>
        /// <param name="receiveBuffer">
        /// The buffer into which data is to be received.
        /// </param>
        /// <param name="flags">
        /// The socket flags associated with the send operation.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to observe during the asynchronous operation.
        /// </param>
        /// <returns>
        /// The result of the asynchronous receive operation.
        /// </returns>
        public abstract ValueTask<TransmissionResult> ReceiveAsync(in EndPoint remoteEndPoint, Memory<byte> receiveBuffer,
            SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the data in the given buffer to the specified endpoint. On connection-oriented protocols, the given endpoint is ignored in favour of
        /// the default remote host set up by a call to <see cref="Connect" /> or <see cref="ConnectAsync" />.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint to which data should be sent. Ignored on connection-oriented protocols.
        /// </param>
        /// <param name="sendBuffer">
        /// The buffer containing the outgoing data to be sent.
        /// </param>
        /// <param name="flags">
        /// The socket flags associated with the send operation.
        /// </param>
        /// <returns>
        /// The result of the send operation.
        /// </returns>
        public abstract TransmissionResult Send(in EndPoint remoteEndPoint, byte[] sendBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Asynchronously sends the data in the given buffer to the specified endpoint. On connection-oriented protocols, the given endpoint is
        /// ignored in favour of the default remote host set up by a call to <see cref="Connect" /> or <see cref="ConnectAsync" />.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint to which data should be sent. Ignored on connection-oriented protocols.
        /// </param>
        /// <param name="sendBuffer">
        /// The buffer containing the outgoing data to be sent. The contents of the buffer are copied to an internally maintained buffer when the call
        /// is made.
        /// </param>
        /// <param name="flags">
        /// The socket flags associated with the send operation.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to observe during the asynchronous operation.
        /// </param>
        /// <returns>
        /// The result of the asynchronous send operation.
        /// </returns>
        public abstract ValueTask<TransmissionResult> SendAsync(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> sendBuffer,
            SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default);

        /// <summary>
        /// A state token for asynchronous socket operations.
        /// </summary>
        protected readonly struct AsyncOperationToken
        {
            /// <summary>
            /// The <see cref="System.Threading.CancellationToken" /> associated with the socket operation.
            /// </summary>
            public readonly CancellationToken CancellationToken;

            /// <summary>
            /// The completion source which wraps the event-based APM, and provides an awaitable <see cref="Task" />.
            /// </summary>
            public readonly TaskCompletionSource<bool> CompletionSource;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncOperationToken" /> struct.
            /// </summary>
            /// <param name="completionSource">
            /// The completion source to trigger when the socket operation completes.
            /// </param>
            /// <param name="cancellationToken">
            /// The cancellation token to observe during the operation.
            /// </param>
            public AsyncOperationToken(in TaskCompletionSource<bool> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// A state token for asynchronous incoming network IO operations.
        /// </summary>
        protected readonly struct AsyncReceiveToken
        {
            /// <summary>
            /// The <see cref="System.Threading.CancellationToken" /> associated with the network IO operation.
            /// </summary>
            public readonly CancellationToken CancellationToken;

            /// <summary>
            /// The completion source which wraps the event-based APM, and provides an awaitable <see cref="Task" />.
            /// </summary>
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncReceiveToken" /> struct.
            /// </summary>
            /// <param name="completionSource">
            /// The completion source to trigger when the IO operation completes.
            /// </param>
            /// <param name="cancellationToken">
            /// The cancellation token to observe during the operation.
            /// </param>
            public AsyncReceiveToken(in TaskCompletionSource<TransmissionResult> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// A state token for asynchronous outgoing network IO operations.
        /// </summary>
        protected readonly struct AsyncSendToken
        {
            /// <summary>
            /// The <see cref="System.Threading.CancellationToken" /> associated with the network IO operation.
            /// </summary>
            public readonly CancellationToken CancellationToken;

            /// <summary>
            /// The completion source which wraps the event-based APM, and provides an awaitable <see cref="Task" />.
            /// </summary>
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            /// <summary>
            /// The rented buffer which holds the user's data.
            /// </summary>
            public readonly byte[] RentedBuffer;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncSendToken" /> struct.
            /// </summary>
            /// <param name="completionSource">
            /// The completion source to trigger when the IO operation completes.
            /// </param>
            /// <param name="rentedBuffer">
            /// The buffer holding the user's data.
            /// </param>
            /// <param name="cancellationToken">
            /// The cancellation token to observe during the operation.
            /// </param>
            public AsyncSendToken(in TaskCompletionSource<TransmissionResult> completionSource, in byte[] rentedBuffer, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                RentedBuffer = rentedBuffer;

                CancellationToken = cancellationToken;
            }
        }
    }
}