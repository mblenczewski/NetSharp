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
    public sealed class SocketReader
    {
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

            receiveAsyncEventArgsPool = new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjects));

            for (int i = 0; i < maxPooledObjects; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += HandleIOCompleted;
                receiveAsyncEventArgsPool.Return(args);
            }
        }

        public int PacketBufferLength { get; }

        public Task<TransmissionResult> ReceiveAsync(Socket socket, SocketFlags socketFlags,
            Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveBuffer = receiveBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedReceiveBufferMemory = new Memory<byte>(rentedReceiveBuffer);

            SocketAsyncEventArgs args = receiveAsyncEventArgsPool.Get();
            args.SetBuffer(rentedReceiveBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncReadToken(rentedReceiveBuffer, outputBuffer, tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(outputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveBufferPool.Return(rentedReceiveBuffer, true);
            receiveAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        public Task<TransmissionResult> ReceiveFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveFromBuffer = receiveBufferPool.Rent(PacketBufferLength);
            Memory<byte> rentedReceiveFromBufferMemory = new Memory<byte>(rentedReceiveFromBuffer);

            SocketAsyncEventArgs args = receiveAsyncEventArgsPool.Get();
            args.SetBuffer(rentedReceiveFromBufferMemory);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncReadToken(rentedReceiveFromBuffer, outputBuffer, tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveFromAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(outputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveBufferPool.Return(rentedReceiveFromBuffer, true);
            receiveAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }
    }
}