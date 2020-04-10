using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    public static class SocketAsyncOperations
    {
        /// <summary>
        /// Event handler for the <see cref="SocketAsyncEventArgs.Completed"/> event.
        /// </summary>
        /// <param name="sender">The object on which the event is raised.</param>
        /// <param name="args">The event arguments.</param>
        public static void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    AsyncAcceptToken asyncAcceptToken = (AsyncAcceptToken)args.UserToken;

                    if (asyncAcceptToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncAcceptToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncAcceptToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            asyncAcceptToken.CompletionSource.SetResult(true);
                        }
                    }

                    break;

                case SocketAsyncOperation.Connect:
                    AsyncConnectToken asyncConnectToken = (AsyncConnectToken)args.UserToken;

                    if (asyncConnectToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncConnectToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncConnectToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            asyncConnectToken.CompletionSource.SetResult(true);
                        }
                    }

                    break;

                case SocketAsyncOperation.Disconnect:
                    AsyncDisconnectToken asyncDisconnectToken = (AsyncDisconnectToken)args.UserToken;

                    if (asyncDisconnectToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncDisconnectToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncDisconnectToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            asyncDisconnectToken.CompletionSource.SetResult(true);
                        }
                    }

                    break;

                case SocketAsyncOperation.Receive:
                    AsyncReadToken asyncReceiveToken = (AsyncReadToken)args.UserToken;

                    if (asyncReceiveToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncReceiveToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncReceiveToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else if (args.BytesTransferred > 0)
                        {
                            TransmissionResult result = new TransmissionResult(in args);

                            asyncReceiveToken.CompletionSource.SetResult(result);
                        }
                        else
                        {
                            asyncReceiveToken.CompletionSource.SetException(
                                new Exception($"Receive method received 0 bytes from remote endpoint!"));
                        }
                    }

                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    AsyncReadFromToken asyncReceiveFromToken = (AsyncReadFromToken)args.UserToken;

                    if (asyncReceiveFromToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncReceiveFromToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncReceiveFromToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            TransmissionResult result = new TransmissionResult(in args);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                    }

                    break;

                case SocketAsyncOperation.Send:
                    AsyncWriteToken asyncSendToken = (AsyncWriteToken)args.UserToken;

                    if (asyncSendToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncSendToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncSendToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            TransmissionResult result = new TransmissionResult(in args);

                            asyncSendToken.CompletionSource.SetResult(result);
                        }
                    }

                    break;

                case SocketAsyncOperation.SendTo:
                    AsyncWriteToToken asyncSendToToken = (AsyncWriteToToken)args.UserToken;

                    if (asyncSendToToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncSendToToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (args.SocketError != SocketError.Success)
                        {
                            asyncSendToToken.CompletionSource.SetException(
                                new SocketException((int)args.SocketError));
                        }
                        else
                        {
                            TransmissionResult result = new TransmissionResult(in args);

                            asyncSendToToken.CompletionSource.SetResult(result);
                        }
                    }

                    break;

                case SocketAsyncOperation.None:
                case SocketAsyncOperation.ReceiveMessageFrom:
                case SocketAsyncOperation.SendPackets:
                    throw new InvalidOperationException(
                        $"{nameof(args.LastOperation)} is not supported by {nameof(SocketAsyncOperations)}");

                default:
                    throw new ArgumentOutOfRangeException(nameof(args.LastOperation),
                        $"Invalid value in the {nameof(args.LastOperation)} enum.");
            }
        }

        public static ValueTask AcceptAsync(SocketAsyncEventArgs clientAcceptArgs, Socket socket,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            clientAcceptArgs.UserToken = new AsyncAcceptToken(tcs, cancellationToken);

            // if the accept operation doesn't complete synchronously, return the awaitable task
            return socket.AcceptAsync(clientAcceptArgs) ? new ValueTask(tcs.Task) : new ValueTask();
        }

        public static ValueTask ConnectAsync(SocketAsyncEventArgs clientConnectArgs, Socket socket, EndPoint remoteEndPoint,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            clientConnectArgs.RemoteEndPoint = remoteEndPoint;
            clientConnectArgs.UserToken = new AsyncConnectToken(tcs, cancellationToken);

            // if the connect operation doesn't complete synchronously, return the awaitable task
            return socket.ConnectAsync(clientConnectArgs) ? new ValueTask(tcs.Task) : new ValueTask();
        }

        public static ValueTask DisconnectAsync(SocketAsyncEventArgs clientDisconnectArgs, Socket socket,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            clientDisconnectArgs.UserToken = new AsyncDisconnectToken(tcs, cancellationToken);

            // if the disconnect operation doesn't complete synchronously, return the awaitable task
            return socket.DisconnectAsync(clientDisconnectArgs) ? new ValueTask(tcs.Task) : new ValueTask();
        }

        public static ValueTask<TransmissionResult> ReceiveAsync(SocketAsyncEventArgs socketArgs, Socket socket, EndPoint remoteEndPoint,
            SocketFlags socketFlags, Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            socketArgs.SetBuffer(inputBuffer);
            socketArgs.SocketFlags = socketFlags;
            socketArgs.RemoteEndPoint = remoteEndPoint;
            socketArgs.UserToken = new AsyncReadToken(tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in socketArgs);

            return new ValueTask<TransmissionResult>(result);
        }

        public static ValueTask<TransmissionResult> ReceiveFromAsync(SocketAsyncEventArgs socketArgs, Socket socket, EndPoint remoteEndPoint,
            SocketFlags socketFlags, Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            socketArgs.SetBuffer(inputBuffer);
            socketArgs.SocketFlags = socketFlags;
            socketArgs.RemoteEndPoint = remoteEndPoint;
            socketArgs.UserToken = new AsyncReadFromToken(tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveFromAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in socketArgs);

            return new ValueTask<TransmissionResult>(result);
        }

        public static ValueTask<TransmissionResult> SendAsync(SocketAsyncEventArgs socketArgs, Socket socket, EndPoint remoteEndPoint,
            SocketFlags socketFlags, Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            socketArgs.SetBuffer(outputBuffer);
            socketArgs.SocketFlags = socketFlags;
            socketArgs.RemoteEndPoint = remoteEndPoint;
            socketArgs.UserToken = new AsyncWriteToken(tcs, cancellationToken);

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in socketArgs);

            return new ValueTask<TransmissionResult>(result);
        }

        public static ValueTask<TransmissionResult> SendToAsync(SocketAsyncEventArgs socketArgs, Socket socket, EndPoint remoteEndPoint,
            SocketFlags socketFlags, Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            socketArgs.SetBuffer(outputBuffer);
            socketArgs.SocketFlags = socketFlags;
            socketArgs.RemoteEndPoint = remoteEndPoint;
            socketArgs.UserToken = new AsyncWriteToToken(tcs, cancellationToken);

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendToAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            TransmissionResult result = new TransmissionResult(in socketArgs);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}