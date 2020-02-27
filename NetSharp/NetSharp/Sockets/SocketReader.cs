using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Helper class providing awaitable wrappers around asynchronous Receive and ReceiveFrom operations.
    /// </summary>
    public sealed class SocketReader
    {
        private readonly int PacketBufferLength;
        private readonly ObjectPool<SocketAsyncEventArgs> receiveAsyncEventArgsPool;
        private readonly ArrayPool<byte> receiveBufferPool;

        private void HandleIOCompleted(object? sender, SocketAsyncEventArgs args)
        {
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
                        else
                        {
                            args.MemoryBuffer.CopyTo(asyncReceiveToken.UserBuffer);

                            TransmissionResult result = new TransmissionResult(args);

                            asyncReceiveToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveBufferPool.Return(asyncReceiveToken.RentedBuffer, true);
                    receiveAsyncEventArgsPool.Return(args);

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
                        else
                        {
                            args.MemoryBuffer.CopyTo(asyncReceiveFromToken.UserBuffer);

                            TransmissionResult result = new TransmissionResult(args);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveBufferPool.Return(asyncReceiveFromToken.RentedBuffer, true);
                    receiveAsyncEventArgsPool.Return(args);

                    break;

                default:
                    throw new InvalidOperationException(
                        $"The {nameof(SocketReader)} class doesn't support the {args.LastOperation} operation.");
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

        internal SocketReader(int packetBufferLength = NetworkPacket.PacketSize, int maxPooledObjects = 10,
            bool preallocateBuffers = false)
        {
            PacketBufferLength = packetBufferLength;

            receiveBufferPool = ArrayPool<byte>.Create(packetBufferLength, maxPooledObjects);

            receiveAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

            for (int i = 0; i < maxPooledObjects; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += HandleIOCompleted;
                receiveAsyncEventArgsPool.Return(args);
            }
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket receive operation.
        /// </summary>
        /// <param name="socket">The socket which should receive data from the remote connection.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <param name="inputBuffer">The memory buffer into which received data will be stored.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The result of the receive operation.</returns>
        public Task<TransmissionResult> ReceiveAsync(Socket socket, SocketFlags socketFlags,
            Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveBuffer = receiveBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedReceiveBufferMemory = new Memory<byte>(rentedReceiveBuffer);

            SocketAsyncEventArgs args = receiveAsyncEventArgsPool.Get();
            args.SetBuffer(rentedReceiveBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncReadToken(rentedReceiveBuffer, inputBuffer, tcs, cancellationToken);

            // register cleanup action for when the cancellation token is thrown
            cancellationToken.Register(() =>
            {
                tcs.SetCanceled();

                receiveBufferPool.Return(rentedReceiveBuffer, true);

                //TODO this is probably a hideous solution. find a better one
                args.Completed -= HandleIOCompleted;
                args.Dispose();

                SocketAsyncEventArgs newArgs = new SocketAsyncEventArgs();
                newArgs.Completed += HandleIOCompleted;
                receiveAsyncEventArgsPool.Return(newArgs);
            });

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(inputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveBufferPool.Return(rentedReceiveBuffer, true);
            receiveAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        /// <summary>
        /// Provides an awaitable wrapper around an asynchronous socket receive operation.
        /// </summary>
        /// <param name="socket">The socket which should receive data from the remote endpoint.</param>
        /// <param name="remoteEndPoint">The remove endpoint from which data should be received.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <param name="inputBuffer">The memory buffer into which received data will be stored.</param>
        /// <param name="cancellationToken">The cancellation token to observe for the operation.</param>
        /// <returns>The result of the receive operation.</returns>
        public Task<TransmissionResult> ReceiveFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveFromBuffer = receiveBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedReceiveFromBufferMemory = new Memory<byte>(rentedReceiveFromBuffer);

            SocketAsyncEventArgs args = receiveAsyncEventArgsPool.Get();
            args.SetBuffer(rentedReceiveFromBufferMemory);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncReadToken(rentedReceiveFromBuffer, inputBuffer, tcs, cancellationToken);

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

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveFromAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(inputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveBufferPool.Return(rentedReceiveFromBuffer, true);
            receiveAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }
    }
}