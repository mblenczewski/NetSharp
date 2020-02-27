using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Packets;

namespace NetSharp.Utils
{
    /// <summary>
    /// Helper class for asynchronously performing common network operations in an efficient manner, for both the UDP and TCP protocols.
    /// </summary>
    public sealed class NetworkOperationsManager
    {
        private readonly ArrayPool<byte> receiveBufferPool;
        private readonly ArrayPool<byte> receiveFromBufferPool;
        private readonly ArrayPool<byte> sendBufferPool;
        private readonly ArrayPool<byte> sendToBufferPool;
        private readonly ObjectPool<SocketAsyncEventArgs> socketAsyncEventArgsPool;

        private void HandleIOCompleted(object? sender, SocketAsyncEventArgs eventArgs)
        {
            bool closed = false;

            switch (eventArgs.LastOperation)
            {
                case SocketAsyncOperation.Send:
                    AsyncWriteToken asyncSendToken = (AsyncWriteToken)eventArgs.UserToken;

                    if (asyncSendToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncSendToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (eventArgs.SocketError != SocketError.Success)
                        {
                            asyncSendToken.CompletionSource.SetException(
                                new SocketException((int)eventArgs.SocketError));
                        }
                        else
                        {
                            asyncSendToken.CompletionSource.SetResult(eventArgs.BytesTransferred);
                        }
                    }

                    sendBufferPool.Return(asyncSendToken.RentedBuffer, true);
                    socketAsyncEventArgsPool.Return(eventArgs);

                    break;

                case SocketAsyncOperation.SendTo:
                    AsyncWriteToken asyncSendToToken = (AsyncWriteToken)eventArgs.UserToken;

                    if (asyncSendToToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncSendToToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (eventArgs.SocketError != SocketError.Success)
                        {
                            asyncSendToToken.CompletionSource.SetException(
                                new SocketException((int)eventArgs.SocketError));
                        }
                        else
                        {
                            asyncSendToToken.CompletionSource.SetResult(eventArgs.BytesTransferred);
                        }
                    }

                    sendToBufferPool.Return(asyncSendToToken.RentedBuffer, true);
                    socketAsyncEventArgsPool.Return(eventArgs);

                    break;

                case SocketAsyncOperation.Receive:
                    AsyncReadToken asyncReceiveToken = (AsyncReadToken)eventArgs.UserToken;

                    if (asyncReceiveToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncReceiveToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (eventArgs.SocketError != SocketError.Success)
                        {
                            asyncReceiveToken.CompletionSource.SetException(
                                new SocketException((int)eventArgs.SocketError));
                        }
                        else
                        {
                            eventArgs.MemoryBuffer.CopyTo(asyncReceiveToken.UserBuffer);

                            TransmissionResult result = new TransmissionResult(eventArgs);

                            asyncReceiveToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveBufferPool.Return(asyncReceiveToken.RentedBuffer, true);

                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    AsyncReadToken asyncReceiveFromToken = (AsyncReadToken)eventArgs.UserToken;

                    if (asyncReceiveFromToken.CancellationToken.IsCancellationRequested)
                    {
                        asyncReceiveFromToken.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        if (eventArgs.SocketError != SocketError.Success)
                        {
                            asyncReceiveFromToken.CompletionSource.SetException(
                                new SocketException((int)eventArgs.SocketError));
                        }
                        else
                        {
                            eventArgs.MemoryBuffer.CopyTo(asyncReceiveFromToken.UserBuffer);
                            TransmissionResult result = new TransmissionResult(eventArgs);

                            asyncReceiveFromToken.CompletionSource.SetResult(result);
                        }
                    }

                    receiveFromBufferPool.Return(asyncReceiveFromToken.RentedBuffer, true);

                    break;

                case SocketAsyncOperation.Disconnect:
                    closed = true;
                    break;

                case SocketAsyncOperation.Accept:
                case SocketAsyncOperation.Connect:
                case SocketAsyncOperation.ReceiveMessageFrom:
                case SocketAsyncOperation.SendPackets:
                case SocketAsyncOperation.None:
                    throw new NotImplementedException();

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (closed)
            {
                // handle the client closing the connection on tcp servers at some point
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

        public const int DefaultMaximumPooledObjects = 10;

        public NetworkOperationsManager(int bufferSize = NetworkPacket.PacketSize,
            int maxPooledObjectCount = DefaultMaximumPooledObjects, bool preallocateBuffers = false)
        {
            MaxPooledObjects = maxPooledObjectCount;
            BufferSize = bufferSize;

            sendBufferPool = ArrayPool<byte>.Create(bufferSize, maxPooledObjectCount);
            receiveBufferPool = ArrayPool<byte>.Create(bufferSize, maxPooledObjectCount);

            sendToBufferPool = ArrayPool<byte>.Create(bufferSize, maxPooledObjectCount);
            receiveFromBufferPool = ArrayPool<byte>.Create(bufferSize, maxPooledObjectCount);

            socketAsyncEventArgsPool = new LeakTrackingObjectPool<SocketAsyncEventArgs>(
                new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>(),
                    maxPooledObjectCount));

            for (int i = 0; i < MaxPooledObjects; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += HandleIOCompleted;

                socketAsyncEventArgsPool.Return(args);
            }

            if (preallocateBuffers)
            {
                // TODO: Allocate and return array pool buffers
            }
        }

        public int BufferSize { get; }

        public int MaxPooledObjects { get; }

        public Task<TransmissionResult> ReceiveAsync(Socket socket, SocketFlags socketFlags, Memory<byte> outputBuffer,
                            CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveBuffer = receiveBufferPool.Rent(BufferSize);
            Memory<byte> rentedReceiveBufferMemory = new Memory<byte>(rentedReceiveBuffer);

            SocketAsyncEventArgs args = socketAsyncEventArgsPool.Get();

            args.SetBuffer(rentedReceiveBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncReadToken(rentedReceiveBuffer, outputBuffer, tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(outputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveBufferPool.Return(rentedReceiveBuffer, true);

            return Task.FromResult(result);
        }

        public Task<TransmissionResult> ReceiveFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            Memory<byte> outputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<TransmissionResult> tcs = new TaskCompletionSource<TransmissionResult>();

            byte[] rentedReceiveFromBuffer = receiveFromBufferPool.Rent(BufferSize);
            Memory<byte> rentedReceiveFromBufferMemory = new Memory<byte>(rentedReceiveFromBuffer);

            SocketAsyncEventArgs args = socketAsyncEventArgsPool.Get();

            args.SetBuffer(rentedReceiveFromBufferMemory);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new AsyncReadToken(rentedReceiveFromBuffer, outputBuffer, tcs, cancellationToken);

            // if the receive operation doesn't complete synchronously, returns the awaitable task
            if (socket.ReceiveFromAsync(args)) return tcs.Task;

            args.MemoryBuffer.CopyTo(outputBuffer);

            TransmissionResult result = new TransmissionResult(args);

            receiveFromBufferPool.Return(rentedReceiveFromBuffer, true);

            return Task.FromResult(result);
        }

        public Task<int> SendAsync(Socket socket, SocketAsyncEventArgs receiveArgs, SocketFlags socketFlags,
            Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            byte[] rentedSendBuffer = sendBufferPool.Rent(BufferSize);
            Memory<byte> rentedSendBufferMemory = new Memory<byte>(rentedSendBuffer);

            inputBuffer.CopyTo(rentedSendBufferMemory);

            SocketAsyncEventArgs args = receiveArgs;
            args.SetBuffer(rentedSendBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncWriteToken(rentedSendBuffer, tcs, cancellationToken);

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendAsync(args)) return tcs.Task;

            int result = args.BytesTransferred;

            sendBufferPool.Return(rentedSendBuffer, true);
            socketAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }

        public Task<int> SendToAsync(Socket socket, SocketAsyncEventArgs receiveFromArgs, SocketFlags socketFlags,
            Memory<byte> inputBuffer, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

            byte[] rentedSendToBuffer = sendToBufferPool.Rent(BufferSize);
            Memory<byte> rentedSendToBufferMemory = new Memory<byte>(rentedSendToBuffer);

            inputBuffer.CopyTo(rentedSendToBufferMemory);

            SocketAsyncEventArgs args = receiveFromArgs;
            args.SetBuffer(rentedSendToBufferMemory);
            args.SocketFlags = socketFlags;
            args.UserToken = new AsyncWriteToken(rentedSendToBuffer, tcs, cancellationToken);

            // if the send operation doesn't complete synchronously, return the awaitable task
            if (socket.SendToAsync(args)) return tcs.Task;

            int result = args.BytesTransferred;

            sendToBufferPool.Return(rentedSendToBuffer, true);
            socketAsyncEventArgsPool.Return(args);

            return Task.FromResult(result);
        }
    }
}