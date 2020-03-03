using System;
using System.Buffers;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharp
{
    /// <summary>
    /// Implements low-level network access on top of which the rest of the connection is built upon.
    /// </summary>
    public sealed partial class Connection : IDisposable
    {
        /// <summary>
        /// Represents any remote endpoint for datagram operations.
        /// </summary>
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly ObjectPool<SocketAsyncEventArgs> acceptArgsPool;

        private readonly ObjectPool<SocketAsyncEventArgs> clientSocketArgsPool;

        private readonly ObjectPool<SocketAsyncEventArgs> connectArgsPool;

        private readonly Socket datagramSocket;

        private readonly ObjectPool<SocketAsyncEventArgs> disconnectArgsPool;

        private readonly ObjectPool<SocketAsyncEventArgs> receiveArgsPool;

        private readonly ArrayPool<byte> receiveFromBufferPool;

        private readonly ObjectPool<SocketAsyncEventArgs> sendArgsPool;

        private readonly ArrayPool<byte> sendToBufferPool;

        private readonly CancellationTokenSource serverShutdownTokenSource;

        private readonly Socket streamSocket;

        /// <summary>
        /// Socket async event args object used when the connection instance is used as a client. It is set when a
        /// call to <see cref="TryConnectAsync"/> is made.
        /// </summary>
        private volatile SocketAsyncEventArgs? connectionAsyncEventArgs;

        /// <summary>
        /// Disposes of the managed and unmanaged resources held by this instance.
        /// </summary>
        /// <param name="disposing">Whether this method is called by <see cref="Dispose()"/> or by the finaliser.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                serverShutdownTokenSource.Cancel();
                serverShutdownTokenSource.Dispose();

                streamSocket.Dispose();
                datagramSocket.Dispose();
            }
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket accept operation.
        /// </summary>
        /// <param name="serverSocket">The socket which should be used to accept an incoming connection attempt.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The accepted socket.</returns>
        private Task<SocketAsyncEventArgs> DoAcceptAsync(Socket serverSocket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<SocketAsyncEventArgs> tcs = new TaskCompletionSource<SocketAsyncEventArgs>();

            SocketAsyncEventArgs acceptArgs = acceptArgsPool.Get();
            acceptArgs.AcceptSocket = null;
            acceptArgs.UserToken = new AsyncAcceptToken(tcs, cancellationToken);

            // if the accept operation doesn't complete synchronously, return the awaitable task
            return serverSocket.AcceptAsync(acceptArgs) ? tcs.Task : Task.FromResult(acceptArgs);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket connect operation.
        /// </summary>
        /// <param name="disconnectedSocket">The socket which should asynchronously connect to the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which the socket should connect.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        private Task<SocketAsyncEventArgs> DoConnectAsync(Socket disconnectedSocket, EndPoint remoteEndPoint, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<SocketAsyncEventArgs> tcs = new TaskCompletionSource<SocketAsyncEventArgs>();

            SocketAsyncEventArgs connectArgs = connectArgsPool.Get();
            connectArgs.RemoteEndPoint = remoteEndPoint;
            connectArgs.UserToken = new AsyncConnectToken(tcs, cancellationToken);

            // if the connect operation doesn't complete synchronously, return the awaitable task
            return disconnectedSocket.ConnectAsync(connectArgs) ? tcs.Task : Task.FromResult(connectArgs);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket disconnect operation.
        /// </summary>
        /// <param name="connectedSocket">The socket which should asynchronously disconnect from its remote endpoint.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        private Task DoDisconnectAsync(Socket connectedSocket, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            SocketAsyncEventArgs disconnectArgs = disconnectArgsPool.Get();
            disconnectArgs.DisconnectReuseSocket = true;
            disconnectArgs.UserToken = new AsyncDisconnectToken(tcs, cancellationToken);

            // if the disconnect operation doesn't complete synchronously, return the awaitable task
            if (connectedSocket.DisconnectAsync(disconnectArgs))
            {
                return tcs.Task;
            }

            disconnectArgsPool.Return(disconnectArgs);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket receive operation.
        /// </summary>
        /// <param name="listenerSocket">The socket which should receive data from the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remove endpoint from which data should be received.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <param name="inputBuffer">The memory buffer into which received data will be stored.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The result of the receive operation from the remote endpoint.</returns>
        private Task<TransmissionResult> DoReceiveFromAsync(Socket listenerSocket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveFromBuffer = receiveFromBufferPool.Rent(NetworkPacket.PacketSize);
            Memory<byte> rentedReceiveFromBufferMemory = new Memory<byte>(rentedReceiveFromBuffer);

            SocketAsyncEventArgs clientArgs = receiveArgsPool.Get();
            clientArgs.SetBuffer(rentedReceiveFromBufferMemory);
            clientArgs.SocketFlags = socketFlags;
            clientArgs.RemoteEndPoint = remoteEndPoint;
            clientArgs.UserToken = new AsyncReadToken(rentedReceiveFromBuffer, inputBuffer, tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (listenerSocket.ReceiveFromAsync(clientArgs)) return tcs.Task;

            clientArgs.MemoryBuffer.CopyTo(inputBuffer);

            TransmissionResult result = new TransmissionResult(clientArgs);

            receiveFromBufferPool.Return(rentedReceiveFromBuffer, true);
            receiveArgsPool.Return(clientArgs);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket send operation.
        /// </summary>
        /// <param name="transmitterSocket">The socket which should send the data to the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which data should be written.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        /// <param name="outputBuffer">The data buffer which should be sent.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The result of the send operation to the remote endpoint.</returns>
        private ValueTask<int> DoSendToAsync(Socket transmitterSocket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            byte[] rentedSendToBuffer = sendToBufferPool.Rent(NetworkPacket.PacketSize);
            Memory<byte> rentedSendToBufferMemory = new Memory<byte>(rentedSendToBuffer);

            outputBuffer.CopyTo(rentedSendToBufferMemory);

            SocketAsyncEventArgs clientArgs = sendArgsPool.Get();
            clientArgs.SetBuffer(rentedSendToBufferMemory);
            clientArgs.SocketFlags = socketFlags;
            clientArgs.RemoteEndPoint = remoteEndPoint;
            clientArgs.UserToken = new AsyncWriteToken(rentedSendToBuffer, tcs, cancellationToken);

            /* NOT WORKING, NEED SOLUTION AT SOME POINT!!!
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
            if (transmitterSocket.SendToAsync(clientArgs)) return new ValueTask<int>(tcs.Task);

            int result = clientArgs.BytesTransferred;

            sendToBufferPool.Return(rentedSendToBuffer, true);
            sendArgsPool.Return(clientArgs);

            return new ValueTask<int>(result);
        }

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
                            asyncAcceptToken.CompletionSource.SetResult(args);
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
                            asyncConnectToken.CompletionSource.SetResult(args);
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

                    disconnectArgsPool.Return(args);

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
                            asyncSendToToken.CompletionSource.SetResult(args.BytesTransferred);
                        }
                    }

                    sendToBufferPool.Return(asyncSendToToken.RentedBuffer, true);
                    sendArgsPool.Return(args);

                    break;

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
                        else if (args.BytesTransferred <= 0)
                        {
                            TransmissionResult result = new TransmissionResult(args);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                        else
                        {
                            args.MemoryBuffer.CopyTo(asyncReceiveFromToken.UserBuffer);

                            TransmissionResult result = new TransmissionResult(args);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveFromBufferPool.Return(asyncReceiveFromToken.RentedBuffer, true);
                    receiveArgsPool.Return(args);

                    break;

                default:
                    throw new InvalidOperationException(
                        $"The {nameof(Connection)} class doesn't support the {args.LastOperation} operation.");
            }
        }

        private readonly struct AsyncAcceptToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<SocketAsyncEventArgs> CompletionSource;

            public AsyncAcceptToken(TaskCompletionSource<SocketAsyncEventArgs> tcs, CancellationToken cancellationToken = default)
            {
                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncConnectToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<SocketAsyncEventArgs> CompletionSource;

            public AsyncConnectToken(TaskCompletionSource<SocketAsyncEventArgs> tcs, CancellationToken cancellationToken = default)
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

        private readonly struct AsyncReadToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<TransmissionResult> CompletionSource;
            public readonly byte[] RentedBuffer;
            public readonly Memory<byte> UserBuffer;

            public AsyncReadToken(byte[] rentedBuffer, Memory<byte> userBuffer, TaskCompletionSource<TransmissionResult> tcs,
                CancellationToken cancellationToken = default)
            {
                RentedBuffer = rentedBuffer;
                UserBuffer = userBuffer;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        private readonly struct AsyncWriteToken
        {
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly byte[] RentedBuffer;

            public AsyncWriteToken(byte[] rentedBuffer, TaskCompletionSource<int> tcs,
                CancellationToken cancellationToken = default)
            {
                RentedBuffer = rentedBuffer;

                CompletionSource = tcs;
                CancellationToken = cancellationToken;
            }
        }

        /// <summary>
        /// The maximum number of stream connection that will be accepted.
        /// </summary>
        /// TODO change this to a configurable builder option
        public const int MaximumConnectionBacklog = 10;

        /// <summary>
        /// The maximum number of packets that will be stored before older packets start to be dropped.
        /// </summary>
        /// TODO change this to a configurable builder option
        public const int MaximumPacketBacklog = 64;

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Task<TransmissionResult> ReceiveAsync(Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            if (connectionAsyncEventArgs == null)
            {
                throw new ConstraintException(
                    $"{nameof(TryConnectAsync)} has not yet been called, or a valid connection has not been made.");
            }

            return DoReceiveFromAsync(connectionAsyncEventArgs.ConnectSocket, connectionAsyncEventArgs.ConnectSocket.RemoteEndPoint, flags, inputBuffer, cts.Token);
        }

        public Task<TransmissionResult> ReceiveFromAsync(EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return DoReceiveFromAsync(datagramSocket, remoteEndPoint, flags, inputBuffer, cts.Token);
        }

        public ValueTask<int> SendAsync(Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            if (connectionAsyncEventArgs == null)
            {
                throw new ConstraintException(
                    $"{nameof(TryConnectAsync)} has not yet been called, or a valid connection has not been made.");
            }

            return DoSendToAsync(connectionAsyncEventArgs.ConnectSocket, connectionAsyncEventArgs.ConnectSocket.RemoteEndPoint, flags, outputBuffer, cts.Token);
        }

        public ValueTask<int> SendToAsync(EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            return DoSendToAsync(datagramSocket, remoteEndPoint, flags, outputBuffer, cts.Token);
        }

        /// <summary>
        /// Configures the logger to log messages to the given stream (or to <see cref="Stream.Null"/> if <c>null</c>) and
        /// to only log messages that are of severity <paramref name="minimumLoggedSeverity"/> or higher.
        /// </summary>
        /// <param name="loggingStream">The stream to which messages will be logged.</param>
        /// <param name="minimumLoggedSeverity">The minimum severity a message must be to be logged.</param>
        public void SetLoggingStream(Stream? loggingStream, LogLevel minimumLoggedSeverity = LogLevel.Info)
        {
            lock (loggerLockObject)
            {
                logger = new Logger(loggingStream ?? Stream.Null, minimumLoggedSeverity);
            }
        }

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local endpoint. Does not block.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public async Task<bool> TryBindAsync(EndPoint localEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            try
            {
                return await Task.Run(() =>
                {
                    streamSocket.Bind(localEndPoint);
                    datagramSocket.Bind(localEndPoint);

                    return true;
                }, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on binding socket to {localEndPoint}:", ex);
                return false;
            }
        }

        public async Task<bool> TryConnectAsync(EndPoint remoteEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            try
            {
                connectionAsyncEventArgs = await DoConnectAsync(streamSocket, remoteEndPoint, cts.Token);

                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on connecting socket to {remoteEndPoint}:", ex);
                return false;
            }
        }

        public async Task<bool> TryDisconnectAsync(TimeSpan timeout)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, ServerShutdownToken);

            try
            {
                if (connectionAsyncEventArgs == null) return false;

                streamSocket.Shutdown(SocketShutdown.Both);
                streamSocket.Close(1);

                await DoDisconnectAsync(streamSocket, cts.Token);

                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on disconnecting socket:", ex);
                return false;
            }
        }
    }
}