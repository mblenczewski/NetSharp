using NetSharp.Packets;
using NetSharp.Utils;

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Stream
{
    /// <summary>
    /// Provides additional configuration options for a <see cref="StreamSocketClient" /> instance.
    /// </summary>
    public readonly struct StreamSocketClientOptions
    {
        /// <summary>
        /// The default configuration.
        /// </summary>
        public static readonly StreamSocketClientOptions Defaults =
            new StreamSocketClientOptions(0);

        /// <summary>
        /// The number of <see cref="SocketAsyncEventArgs" /> instances that should be preallocated for use in the
        /// <see cref="StreamSocketClient.SendAsync" /> and <see cref="StreamSocketClient.ReceiveAsync" /> methods.
        /// </summary>
        public readonly ushort PreallocatedTransmissionArgs;

        /// <summary>
        /// Constructs a new instance of the <see cref="StreamSocketClientOptions" /> struct.
        /// </summary>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of <see cref="SocketAsyncEventArgs" /> instances to preallocate.
        /// </param>
        public StreamSocketClientOptions(ushort preallocatedTransmissionArgs)
        {
            PreallocatedTransmissionArgs = preallocatedTransmissionArgs;
        }
    }

    //TODO address the need to handle series of network packets, not just single packets
    //TODO document class
    public sealed class StreamSocketClient : SocketClient
    {
        private readonly StreamSocketClientOptions clientOptions;

        public StreamSocketClient(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in StreamSocketClientOptions? clientOptions = null) : base(in connectionAddressFamily, SocketType.Stream, in connectionProtocolType,
            NetworkPacket.TotalSize, clientOptions?.PreallocatedTransmissionArgs ?? StreamSocketClientOptions.Defaults.PreallocatedTransmissionArgs)
        {
            this.clientOptions = clientOptions ?? StreamSocketClientOptions.Defaults;
        }

        public ref readonly StreamSocketClientOptions ClientOptions
        {
            get { return ref clientOptions; }
        }

        private void CompleteConnect(SocketAsyncEventArgs args)
        {
            AsyncOperationToken connectToken = (AsyncOperationToken)args.UserToken;

            if (connectToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    connectToken.CompletionSource.SetResult(true);

                    break;

                case SocketError.OperationAborted:
                    break;

                default:
                    connectToken.CompletionSource.SetException(new SocketException((int)args.SocketError));

                    break;
            }

            TransmissionArgsPool.Return(args);
        }

        private void CompleteDisconnect(SocketAsyncEventArgs args)
        {
            AsyncOperationToken disconnectToken = (AsyncOperationToken)args.UserToken;

            if (disconnectToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    disconnectToken.CompletionSource.SetResult(true);

                    break;

                case SocketError.OperationAborted:
                    break;

                default:
                    disconnectToken.CompletionSource.SetException(new SocketException((int)args.SocketError));

                    break;
            }

            TransmissionArgsPool.Return(args);
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            AsyncReceiveToken receiveToken = (AsyncReceiveToken)args.UserToken;

            if (receiveToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    Memory<byte> transmissionBuffer = args.MemoryBuffer;
                    int expectedBytes = transmissionBuffer.Length;

                    if (args.BytesTransferred == expectedBytes)
                    {
                        // buffer was fully received

                        TransmissionResult result = new TransmissionResult(in args);

                        receiveToken.CompletionSource.SetResult(result);
                    }
                    else if (expectedBytes > args.BytesTransferred && args.BytesTransferred > 0)
                    {
                        // receive the remaining parts of the buffer

                        int receivedBytes = args.BytesTransferred;

                        args.SetBuffer(receivedBytes, expectedBytes - receivedBytes);

                        Connection.ReceiveAsync(args);
                        return;
                    }
                    else
                    {
                        // no bytes were received, remote socket is dead

                        receiveToken.CompletionSource.SetException(new SocketException((int)SocketError.HostDown));
                    }

                    break;

                case SocketError.OperationAborted:
                    break;

                default:
                    receiveToken.CompletionSource.SetException(new SocketException((int)args.SocketError));
                    break;
            }

            TransmissionArgsPool.Return(args);
        }

        private void CompleteSend(SocketAsyncEventArgs args)
        {
            AsyncSendToken sendToken = (AsyncSendToken)args.UserToken;

            if (sendToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    Memory<byte> transmissionBuffer = args.MemoryBuffer;
                    int remainingBytes = transmissionBuffer.Length;

                    if (args.BytesTransferred == remainingBytes)
                    {
                        // buffer was fully sent

                        TransmissionResult result = new TransmissionResult(in args);

                        sendToken.CompletionSource.SetResult(result);
                    }
                    else if (remainingBytes > args.BytesTransferred && args.BytesTransferred > 0)
                    {
                        // send the remaining parts of the buffer

                        int sentBytes = args.BytesTransferred;

                        args.SetBuffer(sentBytes, remainingBytes - sentBytes);

                        Connection.SendAsync(args);
                        return;
                    }
                    else
                    {
                        // no bytes were sent, remote socket is dead

                        sendToken.CompletionSource.SetException(new SocketException((int)SocketError.HostDown));
                    }

                    break;

                case SocketError.OperationAborted:
                    break;

                default:
                    sendToken.CompletionSource.SetException(new SocketException((int)args.SocketError));
                    break;
            }

            BufferPool.Return(sendToken.RentedBuffer, true);
            TransmissionArgsPool.Return(args);
        }

        /// <inheritdoc />
        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
        {
            return true;
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs();

            connectionArgs.Completed += HandleIoCompleted;

            return connectionArgs;
        }

        /// <inheritdoc />
        protected override void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.Completed -= HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        /// <inheritdoc />
        protected override void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    CompleteConnect(args);

                    break;

                case SocketAsyncOperation.Disconnect:
                    CompleteDisconnect(args);

                    break;

                case SocketAsyncOperation.Receive:
                    CompleteReceive(args);

                    break;

                case SocketAsyncOperation.Send:
                    CompleteSend(args);

                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        /// <inheritdoc />
        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {
        }

        public void Disconnect(bool allowSocketReuse)
        {
            Connection.Disconnect(allowSocketReuse);
        }

        public ValueTask DisconnectAsync(bool allowSocketReuse, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.DisconnectReuseSocket = allowSocketReuse;
            args.UserToken = new AsyncOperationToken(in tcs, in cancellationToken);

            // TODO find out why the fricc we leak memory
            CancellationTokenRegistration cancellationRegistration =
                cancellationToken.Register(CancelAsyncOperationCallback, args);

            if (Connection.DisconnectAsync(args))
                return new ValueTask(
                    tcs.Task.ContinueWith((task, state) =>
                    {
                        ((CancellationTokenRegistration)state).Dispose();

                        return task.Result;
                    }, cancellationRegistration, CancellationToken.None)
                );

            cancellationRegistration.Dispose();

            TransmissionArgsPool.Return(args);

            return new ValueTask();
        }

        public TransmissionResult Receive(byte[] buffer, SocketFlags flags = SocketFlags.None)
        {
            int expectedBytes = buffer.Length;
            int receivedBytes = 0;

            do
            {
                receivedBytes += Connection.Receive(buffer, receivedBytes, expectedBytes - receivedBytes, flags);
            } while (receivedBytes != 0 && receivedBytes < expectedBytes);

            return new TransmissionResult(in buffer, in receivedBytes, Connection.RemoteEndPoint);
        }

        public ValueTask<TransmissionResult> ReceiveAsync(Memory<byte> receiveBuffer, SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.SetBuffer(receiveBuffer);

            args.SocketFlags = flags;
            args.UserToken = new AsyncReceiveToken(in tcs, in cancellationToken);

            if (cancellationToken == default)
            {
                if (Connection.ReceiveAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);
            }
            else
            {
                // TODO find out why the fricc we leak memory
                CancellationTokenRegistration cancellationRegistration =
                    cancellationToken.Register(CancelAsyncTransmissionCallback, args);

                if (Connection.ReceiveAsync(args))
                    return new ValueTask<TransmissionResult>(
                        tcs.Task.ContinueWith((task, state) =>
                        {
                            ((CancellationTokenRegistration)state).Dispose();

                            return task.Result;
                        }, cancellationRegistration, CancellationToken.None)
                    );

                cancellationRegistration.Dispose();
            }

            TransmissionResult result = new TransmissionResult(in args);

            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }

        public TransmissionResult Send(byte[] buffer, SocketFlags flags = SocketFlags.None)
        {
            int expectedBytes = buffer.Length;
            int sentBytes = 0;

            do
            {
                sentBytes += Connection.Send(buffer, sentBytes, expectedBytes - sentBytes, flags);
            } while (sentBytes != 0 && sentBytes < expectedBytes);

            return new TransmissionResult(in buffer, in sentBytes, Connection.RemoteEndPoint);
        }

        public ValueTask<TransmissionResult> SendAsync(ReadOnlyMemory<byte> sendBuffer, SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();
            byte[] transmissionBuffer = BufferPool.Rent(sendBuffer.Length);

            sendBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer);

            args.SocketFlags = flags;
            args.UserToken = new AsyncSendToken(in tcs, in transmissionBuffer, in cancellationToken);

            if (cancellationToken == default)
            {
                if (Connection.SendAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);
            }
            else
            {
                // TODO find out why the fricc we leak memory
                CancellationTokenRegistration cancellationRegistration =
                    cancellationToken.Register(CancelAsyncTransmissionCallback, args);

                if (Connection.SendAsync(args))
                    return new ValueTask<TransmissionResult>(
                        tcs.Task.ContinueWith((task, state) =>
                        {
                            ((CancellationTokenRegistration)state).Dispose();

                            return task.Result;
                        }, cancellationRegistration, CancellationToken.None)
                    );

                cancellationRegistration.Dispose();
            }

            TransmissionResult result = new TransmissionResult(in args);

            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}