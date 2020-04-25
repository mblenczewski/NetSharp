using System;
using System.Diagnostics;
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
    /// TODO implement cancellation of ReceiveAsync and ReceiveFromAsync methods.
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

        protected void CancelAsyncTransmissionCallback(object tokenObj)
        {
            Debug.WriteLine("Cancelling asynchronous transmission!");
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
        /// The <see cref="CancellationToken" /> upon whose cancellation the connection attempt should be aborted. TODO make functional
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

            if (Connection.ConnectAsync(args)) return new ValueTask(tcs.Task);

            TransmissionArgsPool.Return(args);

            return new ValueTask();
        }

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
        /// A state token for asynchronous network IO operations.
        /// </summary>
        protected readonly struct AsyncTransmissionToken
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
            /// Constructs a new instance of the <see cref="AsyncTransmissionToken" /> struct.
            /// </summary>
            /// <param name="completionSource">
            /// The completion source to trigger when the IO operation completes.
            /// </param>
            /// <param name="cancellationToken">
            /// The cancellation token to observe during the operation.
            /// </param>
            public AsyncTransmissionToken(in TaskCompletionSource<TransmissionResult> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }
    }
}