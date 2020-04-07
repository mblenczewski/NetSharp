using Microsoft.Extensions.ObjectPool;

using NetSharp.Utils;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Helper class providing awaitable wrappers around asynchronous Receive and ReceiveFrom operations.
    /// </summary>
    public sealed class SocketReader
    {
        private readonly int PacketBufferLength;
        private readonly ObjectPool<SocketAsyncEventArgs> receiveFromAsyncEventArgsPool;
        private readonly ArrayPool<byte> receiveFromBufferPool;

        private void HandleIOCompleted(object? sender, SocketAsyncEventArgs args)
        {
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
                            args.MemoryBuffer.CopyTo(asyncReceiveFromToken.UserBuffer);

                            TransmissionResult result = new TransmissionResult(args);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveFromBufferPool.Return(asyncReceiveFromToken.RentedBuffer, true);
                    receiveFromAsyncEventArgsPool.Return(args);

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

            receiveFromBufferPool = ArrayPool<byte>.Create(packetBufferLength, maxPooledObjects);

            receiveFromAsyncEventArgsPool =
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects);

            for (int i = 0; i < maxPooledObjects; i++)
            {
                SocketAsyncEventArgs receiveFromArgs = new SocketAsyncEventArgs();
                receiveFromArgs.Completed += HandleIOCompleted;
                receiveFromAsyncEventArgsPool.Return(receiveFromArgs);
            }
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

            byte[] rentedReceiveFromBuffer = receiveFromBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedReceiveFromBufferMemory = new Memory<byte>(rentedReceiveFromBuffer);

            SocketAsyncEventArgs args = receiveFromAsyncEventArgsPool.Get();
            args.SetBuffer(rentedReceiveFromBufferMemory);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncReadToken(rentedReceiveFromBuffer, inputBuffer, tcs, cancellationToken);

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
            if (socket.ReceiveFromAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(inputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveFromBufferPool.Return(rentedReceiveFromBuffer, true);
            receiveFromAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }
    }
}