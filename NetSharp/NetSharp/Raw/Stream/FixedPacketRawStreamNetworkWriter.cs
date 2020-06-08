using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Stream
{
    public sealed class FixedPacketRawStreamNetworkWriter : RawStreamNetworkWriter
    {
        private readonly int messageSize;

        /// <inheritdoc />
        public FixedPacketRawStreamNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int messageSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, messageSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            this.messageSize = messageSize;
        }

        /// <inheritdoc />
        protected override void CompleteReceive(SocketAsyncEventArgs args)
        {
            AsyncStreamReadToken token = (AsyncStreamReadToken) args.UserToken;

            byte[] receiveBuffer = args.Buffer;
            int expectedBytes = receiveBuffer.Length;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int receivedBytes = args.BytesTransferred, totalReceivedBytes = token.TotalReadBytes;

                    if (totalReceivedBytes + receivedBytes == expectedBytes)  // transmission complete
                    {
                        receiveBuffer.CopyTo(token.UserBuffer);
                        token.CompletionSource.SetResult(totalReceivedBytes + receivedBytes);
                    }
                    else if (0 < totalReceivedBytes + receivedBytes && totalReceivedBytes + receivedBytes < expectedBytes)  // transmission not complete
                    {
                        // update user token to take account of newly read bytes
                        token = new AsyncStreamReadToken(in token, receivedBytes);
                        args.UserToken = token;

                        args.SetBuffer(totalReceivedBytes, expectedBytes - receivedBytes);

                        ContinueReceive(args);
                        return;
                    }
                    else if (receivedBytes == 0)  // connection is dead
                    {
                        token.CompletionSource.SetException(new SocketException((int) SocketError.HostDown));
                    }
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int) args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(receiveBuffer, true);
            ArgsPool.Return(args);
        }

        /// <inheritdoc />
        protected override void CompleteSend(SocketAsyncEventArgs args)
        {
            AsyncStreamWriteToken token = (AsyncStreamWriteToken) args.UserToken;

            byte[] sendBuffer = args.Buffer;
            int expectedBytes = sendBuffer.Length;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int sentBytes = args.BytesTransferred, totalSentBytes = token.TotalWrittenBytes;

                    if (totalSentBytes + sentBytes == expectedBytes) // transmission complete
                    {
                        token.CompletionSource.SetResult(totalSentBytes + sentBytes);
                    }
                    else if (0 < totalSentBytes + sentBytes && totalSentBytes + sentBytes < expectedBytes)  // transmission not complete
                    {
                        // update user token to take account of newly written bytes
                        token = new AsyncStreamWriteToken(in token, sentBytes);
                        args.UserToken = token;

                        args.SetBuffer(totalSentBytes, expectedBytes - sentBytes);

                        ContinueSend(args);
                        return;
                    }
                    else if (sentBytes == 0)  // connection is dead
                    {
                        token.CompletionSource.SetException(new SocketException((int) SocketError.HostDown));
                    }
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int) args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(sendBuffer, true);
            ArgsPool.Return(args);
        }

        /// <inheritdoc />
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > messageSize)
            {
                throw new ArgumentException(
                    $"Cannot receive a message of size: {totalBytes} bytes; maximum message size: {messageSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            int readBytes = 0;

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);

            do
            {
                readBytes += Connection.Receive(transmissionBuffer, readBytes, totalBytes - readBytes, flags);
            } while (readBytes < totalBytes && readBytes != 0);

            transmissionBuffer.CopyTo(readBuffer);
            BufferPool.Return(transmissionBuffer, true);

            return readBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > messageSize)
            {
                throw new ArgumentException(
                    $"Cannot receive a message of size: {totalBytes} bytes; maximum message size: {messageSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);

            args.SetBuffer(transmissionBuffer, 0, messageSize);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncStreamReadToken token = new AsyncStreamReadToken(tcs, 0, in readBuffer);
            args.UserToken = token;

            if (Connection.ReceiveAsync(args))
            {
                return new ValueTask<int>(tcs.Task);
            }

            CompleteReceive(args);

            return new ValueTask<int>(tcs.Task);
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > messageSize)
            {
                throw new ArgumentException(
                    $"Cannot send a message of size: {totalBytes} bytes; maximum message size: {messageSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            int writtenBytes = 0;

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);
            writeBuffer.CopyTo(transmissionBuffer);

            do
            {
                writtenBytes += Connection.Send(transmissionBuffer, writtenBytes, totalBytes - writtenBytes, flags);
            } while (writtenBytes < totalBytes && writtenBytes != 0);

            BufferPool.Return(transmissionBuffer);

            return writtenBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > messageSize)
            {
                throw new ArgumentException(
                    $"Cannot send a message of size: {totalBytes} bytes; maximum message size: {messageSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);
            writeBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer, 0, messageSize);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncStreamWriteToken token = new AsyncStreamWriteToken(tcs, 0);
            args.UserToken = token;

            if (Connection.SendAsync(args))
            {
                return new ValueTask<int>(tcs.Task);
            }

            CompleteSend(args);

            return new ValueTask<int>(tcs.Task);
        }

        private readonly struct AsyncStreamReadToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly int TotalReadBytes;
            public readonly Memory<byte> UserBuffer;

            public AsyncStreamReadToken(TaskCompletionSource<int> completionSource, int totalReadBytes, in Memory<byte> userBuffer)
            {
                CompletionSource = completionSource;

                TotalReadBytes = totalReadBytes;

                UserBuffer = userBuffer;
            }

            public AsyncStreamReadToken(in AsyncStreamReadToken previousToken, int newlyReadBytes)
            {
                CompletionSource = previousToken.CompletionSource;

                TotalReadBytes = previousToken.TotalReadBytes + newlyReadBytes;

                UserBuffer = previousToken.UserBuffer;
            }
        }

        private readonly struct AsyncStreamWriteToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly int TotalWrittenBytes;

            public AsyncStreamWriteToken(TaskCompletionSource<int> completionSource, int totalWrittenBytes)
            {
                CompletionSource = completionSource;

                TotalWrittenBytes = totalWrittenBytes;
            }

            public AsyncStreamWriteToken(in AsyncStreamWriteToken previousToken, int newlyWrittenBytes)
            {
                CompletionSource = previousToken.CompletionSource;

                TotalWrittenBytes = previousToken.TotalWrittenBytes + newlyWrittenBytes;
            }
        }
    }
}