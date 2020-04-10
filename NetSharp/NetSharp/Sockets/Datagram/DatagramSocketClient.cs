using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Utils;

namespace NetSharp.Sockets.Datagram
{
    //TODO fix memory leak issue
    //TODO address the need to handle series of network packets, not just single packets
    //TODO document class
    public sealed class DatagramSocketClient : SocketClient
    {
        public DatagramSocketClient(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Dgram, in connectionProtocolType)
        {
        }

        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs();

            connectionArgs.Completed += HandleIoCompleted;

            return connectionArgs;
        }

        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {
        }

        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
        {
            return true;
        }

        protected override void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.Completed -= HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        protected override void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    AsyncOperationToken connectToken = (AsyncOperationToken)args.UserToken;

                    if (connectToken.CancellationToken.IsCancellationRequested)
                    {
                        connectToken.CompletionSource.SetCanceled();
                    }
                    else if (args.SocketError == SocketError.Success)
                    {
                        connectToken.CompletionSource.SetResult(true);
                    }
                    else
                    {
                        connectToken.CompletionSource.SetException(new SocketException((int)args.SocketError));
                    }

                    TransmissionArgsPool.Return(args);

                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    AsyncTransmissionToken receiveToken = (AsyncTransmissionToken)args.UserToken;

                    if (receiveToken.CancellationToken.IsCancellationRequested)
                    {
                        receiveToken.CompletionSource.SetCanceled();
                    }
                    else if (args.SocketError == SocketError.Success)
                    {
                        TransmissionResult result = new TransmissionResult(in args);

                        receiveToken.CompletionSource.SetResult(result);
                    }
                    else
                    {
                        receiveToken.CompletionSource.SetException(new SocketException((int)args.SocketError));
                    }

                    TransmissionArgsPool.Return(args);

                    break;

                case SocketAsyncOperation.SendTo:
                    AsyncTransmissionToken sendToken = (AsyncTransmissionToken) args.UserToken;
                    
                    if (sendToken.CancellationToken.IsCancellationRequested)
                    {
                        sendToken.CompletionSource.SetCanceled();
                    }
                    else if (args.SocketError == SocketError.Success)
                    {
                        TransmissionResult result = new TransmissionResult(in args);

                        sendToken.CompletionSource.SetResult(result);
                    }
                    else
                    {
                        sendToken.CompletionSource.SetException(new SocketException((int)args.SocketError));
                    }

                    TransmissionArgsPool.Return(args);

                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        public TransmissionResult ReceiveFrom(ref EndPoint remoteEndPoint, byte[] receiveBuffer, SocketFlags flags = SocketFlags.None)
        {
            int receivedBytes = connection.ReceiveFrom(receiveBuffer, flags, ref remoteEndPoint);

            return new TransmissionResult(in receiveBuffer, in receivedBytes, in remoteEndPoint);
        }

        public ValueTask<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> receiveBuffer,
            SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.SetBuffer(receiveBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;
            args.UserToken = new AsyncTransmissionToken(in tcs, in cancellationToken);

            if (connection.ReceiveFromAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in args);

            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }

        public TransmissionResult SendTo(EndPoint remoteEndPoint, byte[] sendBuffer, SocketFlags flags = SocketFlags.None)
        {
            int sentBytes = connection.SendTo(sendBuffer, flags, remoteEndPoint);

            return new TransmissionResult(in sendBuffer, in sentBytes, in remoteEndPoint);
        }

        public ValueTask<TransmissionResult> SendToAsync(EndPoint remoteEndPoint, Memory<byte> sendBuffer,
            SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.SetBuffer(sendBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;
            args.UserToken = new AsyncTransmissionToken(in tcs, in cancellationToken);

            if (connection.SendToAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in args);
            
            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}