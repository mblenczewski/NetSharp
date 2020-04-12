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
        //TODO document
        protected readonly struct AsyncTransmissionToken
        {
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            public readonly CancellationToken CancellationToken;

            public AsyncTransmissionToken(in TaskCompletionSource<TransmissionResult> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        //TODO document
        protected readonly struct AsyncOperationToken
        {
            public readonly TaskCompletionSource<bool> CompletionSource;

            public readonly CancellationToken CancellationToken;

            public AsyncOperationToken(in TaskCompletionSource<bool> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

        //TODO document
        protected readonly struct AsyncCancellationToken
        {
            public readonly Socket Socket;

            public readonly SocketAsyncEventArgs TransmissionArgs;

            public AsyncCancellationToken(in Socket socket, in SocketAsyncEventArgs args)
            {
                Socket = socket;

                TransmissionArgs = args;
            }
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="SocketClient"/> class.
        /// </summary>
        /// <inheritdoc />
        protected SocketClient(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType,
            in ProtocolType connectionProtocolType, in int maxPooledBufferLength) : base(in connectionAddressFamily,
            in connectionSocketType, in connectionProtocolType, in maxPooledBufferLength)
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
                AsyncCancellationToken cancellationArgs = (AsyncCancellationToken)token;

                Socket.CancelConnectAsync(cancellationArgs.TransmissionArgs);
            }, new AsyncCancellationToken(in Connection, in args));

            if (Connection.ConnectAsync(args)) return new ValueTask(tcs.Task);

            TransmissionArgsPool.Return(args);

            return new ValueTask();
        }
    }
}