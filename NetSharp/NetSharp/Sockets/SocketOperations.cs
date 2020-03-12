using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Utils;

namespace NetSharp.Sockets
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
                case SocketAsyncOperation.ReceiveFrom:
                    AsyncReadToken asyncReceiveFromToken = (AsyncReadToken)args.UserToken;

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

                    asyncReceiveFromToken.ReadArgsPool.Return(args);

                    break;

                case SocketAsyncOperation.SendTo:
                    AsyncWriteToken asyncSendToToken = (AsyncWriteToken)args.UserToken;

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

                    asyncSendToToken.WriteArgsPool.Return(args);
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

                    asyncAcceptToken.AcceptArgsPool.Return(args);

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

                    asyncConnectToken.ConnectArgsPool.Return(args);

                    break;

                case SocketAsyncOperation.Disconnect:
                    AsyncOperationToken asyncDisconnectToken = (AsyncOperationToken)args.UserToken;

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

                    asyncDisconnectToken.OperationArgsPool.Return(args);

                    break;

                default:
                    throw new InvalidOperationException(
                        $"The {nameof(SocketReader)} class doesn't support the {args.LastOperation} operation.");
            }
        }

        private readonly struct AsyncAcceptToken
        {
            public readonly ObjectPool<SocketAsyncEventArgs> AcceptArgsPool;
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<Socket> CompletionSource;

            public AsyncAcceptToken(in ObjectPool<SocketAsyncEventArgs> argsPool, in TaskCompletionSource<Socket> tcs,
                in CancellationToken cancellationToken = default)
            {
                AcceptArgsPool = argsPool;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncConnectToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<Socket> CompletionSource;
            public readonly ObjectPool<SocketAsyncEventArgs> ConnectArgsPool;

            public AsyncConnectToken(in ObjectPool<SocketAsyncEventArgs> argsPool, in TaskCompletionSource<Socket> tcs,
                in CancellationToken cancellationToken = default)
            {
                ConnectArgsPool = argsPool;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncOperationToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<bool> CompletionSource;
            public readonly ObjectPool<SocketAsyncEventArgs> OperationArgsPool;

            public AsyncOperationToken(in ObjectPool<SocketAsyncEventArgs> argsPool, in TaskCompletionSource<bool> tcs,
                in CancellationToken cancellationToken = default)
            {
                OperationArgsPool = argsPool;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncReadToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;
            public readonly ObjectPool<SocketAsyncEventArgs> ReadArgsPool;
            public readonly Memory<byte> UserBuffer;

            public AsyncReadToken(in ObjectPool<SocketAsyncEventArgs> argsPool, in Memory<byte> userBuffer,
                in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
            {
                ReadArgsPool = argsPool;
                UserBuffer = userBuffer;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncWriteToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;
            public readonly ObjectPool<SocketAsyncEventArgs> WriteArgsPool;

            public AsyncWriteToken(in ObjectPool<SocketAsyncEventArgs> argsPool,
                in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
            {
                WriteArgsPool = argsPool;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        public static Task<Socket> AcceptAsync(ObjectPool<SocketAsyncEventArgs> acceptArgsPool,
            Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            SocketAsyncEventArgs args = acceptArgsPool.Get();
            args.UserToken = new AsyncAcceptToken(acceptArgsPool, tcs, cancellationToken);

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
            if (socket.AcceptAsync(args)) return tcs.Task;

            Socket result = args.AcceptSocket;
            args.Completed -= HandleIOCompleted;

            acceptArgsPool.Return(args);

            return Task.FromResult(result);
        }

        public static Task<Socket> ConnectAsync(ObjectPool<SocketAsyncEventArgs> connectArgsPool,
            Socket socket, EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            SocketAsyncEventArgs args = connectArgsPool.Get();
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncConnectToken(connectArgsPool, tcs, cancellationToken);

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
            if (socket.ConnectAsync(args)) return tcs.Task;

            Socket result = args.ConnectSocket;
            args.Completed -= HandleIOCompleted;

            connectArgsPool.Return(args);

            return Task.FromResult(result);
        }

        public static Task DisconnectAsync(ObjectPool<SocketAsyncEventArgs> disconnectArgsPool,
            Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = disconnectArgsPool.Get();
            args.UserToken = new AsyncOperationToken(disconnectArgsPool, tcs, cancellationToken);

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
            if (socket.DisconnectAsync(args)) return tcs.Task;

            args.Completed -= HandleIOCompleted;

            disconnectArgsPool.Return(args);

            return Task.CompletedTask;
        }

        public static ValueTask<TransmissionResult> ReceiveFromAsync(ObjectPool<SocketAsyncEventArgs> receiveArgsPool,
                                    Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags, Memory<byte> inputBuffer,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = receiveArgsPool.Get();
            args.SetBuffer(inputBuffer);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncReadToken(receiveArgsPool, inputBuffer, tcs, cancellationToken);

            args.Completed += HandleIOCompleted;

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
            if (socket.ReceiveFromAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            args.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(args);

            receiveArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }

        public static ValueTask<TransmissionResult> SendToAsync(ObjectPool<SocketAsyncEventArgs> sendArgsPool,
            Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags, Memory<byte> outputBuffer,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            SocketAsyncEventArgs args = sendArgsPool.Get();
            args.SetBuffer(outputBuffer);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncWriteToken(sendArgsPool, tcs, cancellationToken);

            args.Completed += HandleIOCompleted;

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
            if (socket.SendToAsync(args)) return new ValueTask<TransmissionResult>(tcs.Task);

            args.Completed -= HandleIOCompleted;

            TransmissionResult result = new TransmissionResult(args);

            sendArgsPool.Return(args);

            return new ValueTask<TransmissionResult>(result);
        }
    }
}