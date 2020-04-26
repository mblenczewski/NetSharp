﻿using NetSharp.Packets;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Datagram
{
    /// <summary>
    /// Provides additional configuration options for a <see cref="DatagramSocketClient" /> instance.
    /// </summary>
    public readonly struct DatagramSocketClientOptions
    {
        /// <summary>
        /// The default configuration.
        /// </summary>
        public static readonly DatagramSocketClientOptions Defaults =
            new DatagramSocketClientOptions(0);

        /// <summary>
        /// The number of <see cref="SocketAsyncEventArgs" /> instances that should be preallocated for use in the
        /// <see cref="DatagramSocketClient.SendToAsyncInternal" /> and <see cref="DatagramSocketClient.ReceiveFromAsyncInternal" /> methods.
        /// </summary>
        public readonly ushort PreallocatedTransmissionArgs;

        /// <summary>
        /// Constructs a new instance of the <see cref="DatagramSocketClientOptions" /> struct.
        /// </summary>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of <see cref="SocketAsyncEventArgs" /> instances to preallocate.
        /// </param>
        public DatagramSocketClientOptions(ushort preallocatedTransmissionArgs)
        {
            PreallocatedTransmissionArgs = preallocatedTransmissionArgs;
        }
    }

    //TODO address the need to handle series of network packets, not just single packets
    //TODO document class
    public sealed class DatagramSocketClient : SocketClient
    {
        private readonly DatagramSocketClientOptions clientOptions;

        public DatagramSocketClient(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in DatagramSocketClientOptions? clientOptions = null) : base(in connectionAddressFamily, SocketType.Dgram, in connectionProtocolType,
            NetworkPacket.TotalSize, clientOptions?.PreallocatedTransmissionArgs ?? DatagramSocketClientOptions.Defaults.PreallocatedTransmissionArgs)
        {
            this.clientOptions = clientOptions ?? DatagramSocketClientOptions.Defaults;
        }

        public ref readonly DatagramSocketClientOptions ClientOptions
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

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            AsyncReceiveToken receiveToken = (AsyncReceiveToken)args.UserToken;

            if (receiveToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    TransmissionResult result = new TransmissionResult(in args);

                    receiveToken.CompletionSource.SetResult(result);

                    break;

                case SocketError.OperationAborted:
                    break;

                default:
                    receiveToken.CompletionSource.SetException(new SocketException((int)args.SocketError));

                    break;
            }

            TransmissionArgsPool.Return(args);
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            AsyncSendToken sendToken = (AsyncSendToken)args.UserToken;

            if (sendToken.CancellationToken.IsCancellationRequested) return;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    TransmissionResult result = new TransmissionResult(in args);

                    sendToken.CompletionSource.SetResult(result);

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

                case SocketAsyncOperation.ReceiveFrom:
                    CompleteReceiveFrom(args);

                    break;

                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);

                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        /// <inheritdoc />
        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {
        }

        /// <inheritdoc />
        public override TransmissionResult Receive(in EndPoint remoteEndPoint, byte[] receiveBuffer, SocketFlags flags = SocketFlags.None)
        {
            EndPoint actualEndPoint = remoteEndPoint;
            int receivedBytes = Connection.ReceiveFrom(receiveBuffer, flags, ref actualEndPoint);

            return new TransmissionResult(in receiveBuffer, in receivedBytes, in actualEndPoint);
        }

        /// <inheritdoc />
        public override ValueTask<TransmissionResult> ReceiveAsync(in EndPoint remoteEndPoint, Memory<byte> receiveBuffer, SocketFlags flags = SocketFlags.None,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.SetBuffer(receiveBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;
            args.UserToken = new AsyncReceiveToken(in tcs, in cancellationToken);

            if (cancellationToken == default)
            {
                if (Connection.ReceiveFromAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);
            }
            else
            {
                // TODO find out why the fricc we leak memory
                CancellationTokenRegistration cancellationRegistration =
                    cancellationToken.Register(CancelAsyncReceiveCallback, args);

                if (Connection.ReceiveFromAsync(args))
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

        /// <inheritdoc />
        public override TransmissionResult Send(in EndPoint remoteEndPoint, byte[] sendBuffer, SocketFlags flags = SocketFlags.None)
        {
            int sentBytes = Connection.SendTo(sendBuffer, flags, remoteEndPoint);

            return new TransmissionResult(in sendBuffer, in sentBytes, in remoteEndPoint);
        }

        /// <inheritdoc />
        public override ValueTask<TransmissionResult> SendAsync(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> sendBuffer, SocketFlags flags = SocketFlags.None,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();
            byte[] transmissionBuffer = BufferPool.Rent(sendBuffer.Length);

            sendBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;
            args.UserToken = new AsyncSendToken(in tcs, in transmissionBuffer, in cancellationToken);

            if (cancellationToken == default)
            {
                if (Connection.SendToAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);
            }
            else
            {
                // TODO find out why the fricc we leak memory
                CancellationTokenRegistration cancellationRegistration =
                    cancellationToken.Register(CancelAsyncSendCallback, args);

                if (Connection.SendToAsync(args))
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

            BufferPool.Return(transmissionBuffer, true);
            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}