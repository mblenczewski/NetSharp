using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Helper class providing awaitable wrappers around asynchronous Accept, Connect, and Disconnect operations.
    /// </summary>
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
            acceptAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

            connectAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

            disconnectAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

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

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket accept operation.
        /// </summary>
        /// <param name="socket">The socket which should be used to accept an incoming connection attempt.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The accepted socket.</returns>
        public Task<Socket> AcceptAsync(Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<Socket> tcs = new TaskCompletionSource<Socket>();

            SocketAsyncEventArgs args = acceptAsyncEventArgsPool.Get();
            args.UserToken = new AsyncAcceptToken(tcs, cancellationToken);

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

            acceptAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket connect operation.
        /// </summary>
        /// <param name="socket">The socket which should asynchronously connect to the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which the socket should connect.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        public Task ConnectAsync(Socket socket, EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = connectAsyncEventArgsPool.Get();
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncConnectToken(tcs, cancellationToken);

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

            connectAsyncEventArgsPool.Return(args);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket disconnect operation.
        /// </summary>
        /// <param name="socket">The socket which should asynchronously disconnect from its remote endpoint.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        public Task DisconnectAsync(Socket socket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs args = connectAsyncEventArgsPool.Get();
            args.UserToken = new AsyncDisconnectToken(tcs, cancellationToken);

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

            connectAsyncEventArgsPool.Return(args);

            return Task.CompletedTask;
        }
    }
}