using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Packets;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Helper class providing awaitable wrappers around asynchronous Send and SendTo operations.
    /// </summary>
    public sealed class SocketWriter
    {
        private readonly int PacketBufferLength;
        private readonly ObjectPool<SocketAsyncEventArgs> sendAsyncEventArgsPool;
        private readonly ArrayPool<byte> sendBufferPool;

        private void HandleIOCompleted(object? sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
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
                            asyncSendToken.CompletionSource.SetResult(args.BytesTransferred);
                        }
                    }

                    sendBufferPool.Return(asyncSendToken.RentedBuffer, true);
                    sendAsyncEventArgsPool.Return(args);
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

                    sendBufferPool.Return(asyncSendToToken.RentedBuffer, true);
                    sendAsyncEventArgsPool.Return(args);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"The {nameof(SocketWriter)} class doesn't support the {args.LastOperation} operation.");
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

        internal SocketWriter(int packetBufferLength = NetworkPacket.PacketSize, int maxPooledObjects = 10,
            bool preallocateBuffers = false)
        {
            PacketBufferLength = packetBufferLength;

            sendBufferPool = ArrayPool<byte>.Create(packetBufferLength, maxPooledObjects);

            sendAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

            for (int i = 0; i < maxPooledObjects; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += HandleIOCompleted;
                sendAsyncEventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket send operation.
        /// </summary>
        /// <param name="socket">The socket which should send the data to its remote connection.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        /// <param name="outputBuffer">The data buffer which should be sent.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The number of bytes of data which were written to the remote connection.</returns>
        public Task<int> SendAsync(Socket socket, SocketFlags socketFlags, Memory<byte> outputBuffer,
            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            byte[] rentedSendBuffer = sendBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedSendBufferMemory = new Memory<byte>(rentedSendBuffer);

            outputBuffer.CopyTo(rentedSendBufferMemory);

            SocketAsyncEventArgs args = sendAsyncEventArgsPool.Get();
            args.SetBuffer(rentedSendBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncWriteToken(rentedSendBuffer, tcs, cancellationToken);

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendAsync(args)) return tcs.Task;

            int result = args.BytesTransferred;

            sendBufferPool.Return(rentedSendBuffer, true);
            sendAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket send operation.
        /// </summary>
        /// <param name="socket">The socket which should send the data to the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which data should be written.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        /// <param name="outputBuffer">The data buffer which should be sent.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The number of bytes of data which were written to the remote endpoint.</returns>
        public Task<int> SendToAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            byte[] rentedSendToBuffer = sendBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedSendToBufferMemory = new Memory<byte>(rentedSendToBuffer);

            outputBuffer.CopyTo(rentedSendToBufferMemory);

            SocketAsyncEventArgs args = sendAsyncEventArgsPool.Get();
            args.SetBuffer(rentedSendToBufferMemory);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncWriteToken(rentedSendToBuffer, tcs, cancellationToken);

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

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendToAsync(args)) return tcs.Task;

            int result = args.BytesTransferred;

            sendBufferPool.Return(rentedSendToBuffer, true);
            sendAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }
    }
}