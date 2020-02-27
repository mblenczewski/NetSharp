using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace NetSharp.Sockets
{
    public sealed class SocketAcceptor
    {
        private readonly ObjectPool<SocketAsyncEventArgs> acceptAsyncEventArgsPool;
        private readonly ObjectPool<SocketAsyncEventArgs> connectAsyncEventArgsPool;
        private readonly ObjectPool<SocketAsyncEventArgs> disconnectAsyncEventArgsPool;

        private void HandleIOCompleted(object? sender, SocketAsyncEventArgs args)
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
                            asyncAcceptToken.CompletionSource.SetResult(args.AcceptSocket);
                        }
                    }

                    acceptAsyncEventArgsPool.Return(args);

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

                    connectAsyncEventArgsPool.Return(args);

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

                    disconnectAsyncEventArgsPool.Return(args);

                    break;

                default:
                    throw new InvalidOperationException(
                        $"The {nameof(SocketAcceptor)} class doesn't support the {args.LastOperation} operation.");
            }
        }

        private readonly struct AsyncAcceptToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<Socket> CompletionSource;

            public AsyncAcceptToken(TaskCompletionSource<Socket> tcs, CancellationToken cancellationToken = default)
            {
                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncConnectToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<bool> CompletionSource;

            public AsyncConnectToken(TaskCompletionSource<bool> tcs, CancellationToken cancellationToken = default)
            {
                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncDisconnectToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<bool> CompletionSource;

            public AsyncDisconnectToken(TaskCompletionSource<bool> tcs, CancellationToken cancellationToken = default)
            {
                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        internal SocketAcceptor(int maxPooledObjects = 10)
        {
            acceptAsyncEventArgsPool = new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects));

            connectAsyncEventArgsPool = new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects));

            disconnectAsyncEventArgsPool = new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects));

            for (int i = 0; i < maxPooledObjects; i++)
            {
                SocketAsyncEventArgs acceptArgs = new SocketAsyncEventArgs();
                acceptArgs.Completed += HandleIOCompleted;
                acceptAsyncEventArgsPool.Return(acceptArgs);

                SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();
                connectArgs.Completed += HandleIOCompleted;
                connectAsyncEventArgsPool.Return(connectArgs);

                SocketAsyncEventArgs disconnectArgs = new SocketAsyncEventArgs();
                disconnectArgs.Completed += HandleIOCompleted;
                connectAsyncEventArgsPool.Return(disconnectArgs);
            }
        }

        public Task<Socket> AcceptAsync(Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            SocketAsyncEventArgs args = acceptAsyncEventArgsPool.Get();
            args.UserToken = new AsyncAcceptToken(tcs, cancellationToken);

            // if the accept operation doesn't complete synchronously, return the awaitable task
            if (socket.AcceptAsync(args)) return tcs.Task;

            Socket result = args.AcceptSocket;

            acceptAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        public Task ConnectAsync(Socket socket, EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = connectAsyncEventArgsPool.Get();
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncConnectToken(tcs, cancellationToken);

            // if the connect operation doesn't complete synchronously, return the awaitable task
            if (socket.ConnectAsync(args)) return tcs.Task;

            connectAsyncEventArgsPool.Return(args);

            return Task.CompletedTask;
        }

        public Task DisconnectAsync(Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = connectAsyncEventArgsPool.Get();
            args.UserToken = new AsyncDisconnectToken(tcs, cancellationToken);

            // if the disconnect operation doesn't complete synchronously, return the awaitable task
            if (socket.DisconnectAsync(args)) return tcs.Task;

            connectAsyncEventArgsPool.Return(args);

            return Task.CompletedTask;
        }
    }
}