using NetSharp.Utils;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Abstract base class for clients.
    /// </summary>
    public abstract class SocketClient : SocketConnection
    {
        /// <summary>
        /// A state token for asynchronous network IO operations.
        /// </summary>
        protected readonly struct AsyncTransmissionToken
        {
            /// <summary>
            /// The completion source which wraps the event-based APM, and provides an awaitable <see cref="Task"/>.
            /// </summary>
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            /// <summary>
            /// The <see cref="System.Threading.CancellationToken"/> associated with the network IO operation.
            /// </summary>
            public readonly CancellationToken CancellationToken;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncTransmissionToken"/> struct.
            /// </summary>
            /// <param name="completionSource">The completion source to trigger when the IO operation completes.</param>
            /// <param name="cancellationToken">The cancellation token to observe during the operation.</param>
            public AsyncTransmissionToken(in TaskCompletionSource<TransmissionResult> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// A state token for asynchronous socket operations.
        /// </summary>
        protected readonly struct AsyncOperationToken
        {
            /// <summary>
            /// The completion source which wraps the event-based APM, and provides an awaitable <see cref="Task"/>.
            /// </summary>
            public readonly TaskCompletionSource<bool> CompletionSource;

            /// <summary>
            /// The <see cref="System.Threading.CancellationToken"/> associated with the socket operation.
            /// </summary>
            public readonly CancellationToken CancellationToken;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncOperationToken"/> struct.
            /// </summary>
            /// <param name="completionSource">The completion source to trigger when the socket operation completes.</param>
            /// <param name="cancellationToken">The cancellation token to observe during the operation.</param>
            public AsyncOperationToken(in TaskCompletionSource<bool> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// A state token for cancelling asynchronous network IO operations.
        /// </summary>
        protected readonly struct AsyncTransmissionCancellationToken
        {
            /// <summary>
            /// The socket on which the operation was started.
            /// </summary>
            public readonly Socket Socket;

            /// <summary>
            /// The <see cref="SocketAsyncEventArgs"/> instance associated with the network IO operation.
            /// </summary>
            public readonly SocketAsyncEventArgs TransmissionArgs;

            /// <summary>
            /// The pool to which the <see cref="TransmissionArgs"/> should be returned upon operation cancellation.
            /// </summary>
            public readonly SlimObjectPool<SocketAsyncEventArgs> TransmissionArgsPool;

            /// <summary>
            /// The completion source associated with the network IO operation.
            /// </summary>
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncTransmissionCancellationToken"/> struct.
            /// </summary>
            /// <param name="socket">The socket on which the operation was started.</param>
            /// <param name="args">The socket event args associated with the operation.</param>
            /// <param name="argsPool">The pool to which the <paramref name="args"/> instance will be returned upon cancellation.</param>
            /// <param name="completionSource">The completion source associated with the operation.</param>
            public AsyncTransmissionCancellationToken(in Socket socket, in SocketAsyncEventArgs args,
                in SlimObjectPool<SocketAsyncEventArgs> argsPool, in TaskCompletionSource<TransmissionResult> completionSource)
            {
                Socket = socket;

                TransmissionArgs = args;

                TransmissionArgsPool = argsPool;

                CompletionSource = completionSource;
            }
        }

        /// <summary>
        /// A state token for cancelling asynchronous socket operations.
        /// </summary>
        protected readonly struct AsyncOperationCancellationToken
        {
            /// <summary>
            /// The socket on which the operation was started.
            /// </summary>
            public readonly Socket Socket;

            /// <summary>
            /// The <see cref="SocketAsyncEventArgs"/> instance associated with the socket operation.
            /// </summary>
            public readonly SocketAsyncEventArgs TransmissionArgs;

            /// <summary>
            /// The pool to which the <see cref="TransmissionArgs"/> should be returned upon operation cancellation.
            /// </summary>
            public readonly SlimObjectPool<SocketAsyncEventArgs> TransmissionArgsPool;

            /// <summary>
            /// The completion source associated with the network IO operation.
            /// </summary>
            public readonly TaskCompletionSource<bool> CompletionSource;

            /// <summary>
            /// Constructs a new instance of the <see cref="AsyncOperationCancellationToken"/> struct.
            /// </summary>
            /// <param name="socket">The socket on which the operation was started.</param>
            /// <param name="args">The socket event args associated with the operation.</param>
            /// <param name="argsPool">The pool to which the <paramref name="args"/> instance will be returned upon cancellation.</param>
            /// <param name="completionSource">The completion source associated with the operation.</param>
            public AsyncOperationCancellationToken(in Socket socket, in SocketAsyncEventArgs args,
                in SlimObjectPool<SocketAsyncEventArgs> argsPool, in TaskCompletionSource<bool> completionSource)
            {
                Socket = socket;

                TransmissionArgs = args;

                TransmissionArgsPool = argsPool;

                CompletionSource = completionSource;
            }
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <param name="connectionAddressFamily">The address family that the underlying connection should use.</param>
        /// <param name="connectionSocketType">The socket type that the underlying connection should use.</param>
        /// <param name="connectionProtocolType">The protocol type that the underlying connection should use.</param>
        /// <param name="maxPooledBufferLength">The maximum length of a pooled network IO buffer.</param>
        /// <param name="preallocatedTransmissionArgs">The number of transmission args to preallocate.</param>
        protected SocketClient(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType,
            in ProtocolType connectionProtocolType, in int maxPooledBufferLength, in ushort preallocatedTransmissionArgs)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType, in maxPooledBufferLength,
            in preallocatedTransmissionArgs)
        {
        }

        /// <summary>
        /// Connects the client to the specified end point. If called on a <see cref="SocketType.Dgram"/>-based client,
        /// this method configures the default remote host, and the client will ignore any packets not coming from this
        /// default host (i.e the given <paramref name="remoteEndPoint"/>).
        /// </summary>
        /// <param name="remoteEndPoint">The remote end point which to which to connect the client.</param>
        public void Connect(in EndPoint remoteEndPoint)
        {
            Connection.Connect(remoteEndPoint);
        }

        /// <summary>
        /// Asynchronously connects the client to the specified end point. If called on a
        /// <see cref="SocketType.Dgram"/>-based client, this method configures the default remote host, and the client
        /// will ignore any packets not coming from this default host (i.e the given <paramref name="remoteEndPoint"/>).
        /// </summary>
        /// <param name="remoteEndPoint">The remote end point which to which to connect the client.</param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> upon whose cancellation the connection attempt should be aborted.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the connection attempt.</returns>
        public ValueTask ConnectAsync(in EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncOperationToken(in tcs, in cancellationToken);

            cancellationToken.Register(token =>
            {
                AsyncOperationCancellationToken operationCancellationToken = (AsyncOperationCancellationToken)token;

                Socket.CancelConnectAsync(operationCancellationToken.TransmissionArgs);

                operationCancellationToken.CompletionSource.SetCanceled();

                operationCancellationToken.TransmissionArgsPool.Return(operationCancellationToken.TransmissionArgs);
            }, new AsyncOperationCancellationToken(in Connection, in args, in TransmissionArgsPool, in tcs));

            if (Connection.ConnectAsync(args)) return new ValueTask(tcs.Task);

            TransmissionArgsPool.Return(args);

            return new ValueTask();
        }
    }
}