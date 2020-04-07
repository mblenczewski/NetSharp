using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Provides helper awaitable functions for wrapping the <see cref="SocketAsyncEventArgs"/> pattern.
    /// </summary>
    public static class SocketOperations
    {
        private static void HandleIOCompleted(object? sender, SocketAsyncEventArgs args)
        {
            args.Completed -= HandleIOCompleted;

            switch (args.LastOperation)
            {
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
                            TransmissionResult result = new TransmissionResult(args);

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
                            TransmissionResult result = new TransmissionResult(args);

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
                            TransmissionResult result = new TransmissionResult(args);

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
                            TransmissionResult result = new TransmissionResult(args);

                            asyncSendToToken.CompletionSource.SetResult(result);
                        }
                    }

                    break;

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
                            asyncAcceptToken.CompletionSource.SetResult(args.AcceptSocket);
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
                            asyncConnectToken.CompletionSource.SetResult(args.ConnectSocket);
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

                default:
                    throw new InvalidOperationException(
                        $"{nameof(SocketOperations)} doesn't support the {args.LastOperation} operation.");
            }
        }

        private readonly struct AsyncWriteToToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

            public AsyncWriteToToken(in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
            {
                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        public static ValueTask<Socket> AcceptAsync(SocketAsyncEventArgs clientAcceptArgs, Socket socket,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            clientAcceptArgs.UserToken = new AsyncAcceptToken(tcs, cancellationToken);

            clientAcceptArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                acceptAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the accept operation doesn't complete synchronously, return the awaitable task
            if (socket.AcceptAsync(clientAcceptArgs)) return new ValueTask<Socket>(tcs.Task);

            Socket result = clientAcceptArgs.AcceptSocket;
            clientAcceptArgs.Completed -= HandleIOCompleted;

            return new ValueTask<Socket>(result);
        }

        public static ValueTask<Socket> ConnectAsync(SocketAsyncEventArgs clientConnectArgs, Socket socket, EndPoint remoteEndPoint,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            clientConnectArgs.RemoteEndPoint = remoteEndPoint;
            clientConnectArgs.UserToken = new AsyncConnectToken(tcs, cancellationToken);

            clientConnectArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                connectAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the connect operation doesn't complete synchronously, return the awaitable task
            if (socket.ConnectAsync(clientConnectArgs)) return new ValueTask<Socket>(tcs.Task);

            Socket result = clientConnectArgs.ConnectSocket;
            clientConnectArgs.Completed -= HandleIOCompleted;

            return new ValueTask<Socket>(result);
        }

        public static ValueTask DisconnectAsync(SocketAsyncEventArgs clientDisconnectArgs, Socket socket,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            clientDisconnectArgs.UserToken = new AsyncDisconnectToken(tcs, cancellationToken);

            clientDisconnectArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                disconnectAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the disconnect operation doesn't complete synchronously, return the awaitable task
            if (socket.DisconnectAsync(clientDisconnectArgs)) return new ValueTask(tcs.Task);

            clientDisconnectArgs.Completed -= HandleIOCompleted;

            return new ValueTask();
        }

        public static ValueTask<TransmissionResult> ReceiveAsync(SocketAsyncEventArgs socketArgs, Socket socket, EndPoint remoteEndPoint,
            SocketFlags socketFlags, Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            socketArgs.SetBuffer(inputBuffer);
            socketArgs.SocketFlags = socketFlags;
            socketArgs.RemoteEndPoint = remoteEndPoint;
            socketArgs.UserToken = new AsyncReadToken(tcs, cancellationToken);

            socketArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                receiveBufferPool.Return(rentedReceiveFromBuffer, true);

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                receiveAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            socketArgs.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(socketArgs);

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

            socketArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                receiveBufferPool.Return(rentedReceiveFromBuffer, true);

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                receiveAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveFromAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            socketArgs.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(socketArgs);

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

            socketArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                sendBufferPool.Return(rentedSendToBuffer, true);

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                sendAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            socketArgs.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(socketArgs);

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

            socketArgs.Completed += HandleIOCompleted;

            /*
            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                sendBufferPool.Return(rentedSendToBuffer, true);

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                sendAsyncEventArgsPool.Return(newArgs);
            });
            */

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendToAsync(socketArgs)) return new ValueTask<TransmissionResult>(tcs.Task);

            socketArgs.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(socketArgs);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}