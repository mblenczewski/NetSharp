using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Utils;

namespace NetSharp.Sockets.Datagram
{
    //TODO document class
    public sealed class DatagramSocketClient : SocketClient
    {
        private readonly struct SocketOperationToken
        {
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            public readonly CancellationToken CancellationToken;

            public SocketOperationToken(in TaskCompletionSource<TransmissionResult> completionSource, in CancellationToken cancellationToken)
            {
                CompletionSource = completionSource;

                CancellationToken = cancellationToken;
            }
        }

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
                case SocketAsyncOperation.SendTo:
                    SocketOperationToken sendToken = (SocketOperationToken) args.UserToken;
                    
                    if (sendToken.CancellationToken.IsCancellationRequested)
                    {
                        sendToken.CompletionSource.SetCanceled();
                    }
                    else if (args.SocketError == SocketError.Success)
                    {
                        TransmissionResult result = new TransmissionResult(in args);

                        sendToken.CompletionSource.SetResult(result);

                        TransmissionArgsPool.Return(args);
                    }
                    else
                    {
                        sendToken.CompletionSource.SetException(new SocketException((int)args.SocketError));

                        TransmissionArgsPool.Return(args);
                    }

                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    SocketOperationToken receiveToken = (SocketOperationToken)args.UserToken;

                    if (sendToken.CancellationToken.IsCancellationRequested)
                    {
                        receiveToken.CompletionSource.SetCanceled();
                    }
                    else if (args.SocketError == SocketError.Success)
                    {
                        TransmissionResult result = new TransmissionResult(in args);

                        receiveToken.CompletionSource.SetResult(result);

                        TransmissionArgsPool.Return(args);
                    }
                    else
                    {
                        receiveToken.CompletionSource.SetException(new SocketException((int)args.SocketError));

                        TransmissionArgsPool.Return(args);
                    }

                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
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
            args.UserToken = new SocketOperationToken(in tcs, in cancellationToken);

            if (connection.SendToAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in args);
            
            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);

        }

        public TransmissionResult ReceiveFrom(ref EndPoint remoteEndPoint, byte[] receiveBuffer, SocketFlags flags = SocketFlags.None)
        {
            int readBytes = connection.ReceiveFrom(receiveBuffer, flags, ref remoteEndPoint);

            return new TransmissionResult(in receiveBuffer, in readBytes, in remoteEndPoint);
        }

        public ValueTask<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> receiveBuffer,
            SocketFlags flags = SocketFlags.None, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            args.SetBuffer(receiveBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;
            args.UserToken = new SocketOperationToken(in tcs, in cancellationToken);

            if (connection.ReceiveFromAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in args);

            TransmissionArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}