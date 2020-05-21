using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace NetSharp.Raw.Stream
{
    public sealed class VariablePacketRawStreamNetworkWriter : RawStreamNetworkWriter
    {
        //TODO replace as soon as possible
        private const int MESSAGE_SIZE = 8192;

        /// <inheritdoc />
        public VariablePacketRawStreamNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxMessageSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxMessageSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawStreamPacket.Header header)
        {
            byte[] receiveBuffer = BufferPool.Rent(header.DataSize);
            args.SetBuffer(receiveBuffer, 0, header.DataSize);

            AsyncStreamReadToken token = new AsyncStreamReadToken(header.DataSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(RawStreamPacket.Header.TotalHeaderSize);
            args.SetBuffer(receiveBuffer, 0, RawStreamPacket.Header.TotalHeaderSize);

            AsyncStreamReadToken token = new AsyncStreamReadToken(RawStreamPacket.Header.TotalHeaderSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureSendData(SocketAsyncEventArgs args, in RawStreamPacket.Header header)
        {
            byte[] receiveBuffer = BufferPool.Rent(header.DataSize);
            args.SetBuffer(receiveBuffer, 0, header.DataSize);

            AsyncStreamWriteToken token = new AsyncStreamWriteToken(header.DataSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureSendHeader(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(RawStreamPacket.Header.TotalHeaderSize);
            args.SetBuffer(receiveBuffer, 0, RawStreamPacket.Header.TotalHeaderSize);

            AsyncStreamWriteToken token = new AsyncStreamWriteToken(RawStreamPacket.Header.TotalHeaderSize, 0);
            args.UserToken = token;
        }

        /// <inheritdoc />
        protected override void CompleteReceive(SocketAsyncEventArgs args)
        {
            AsyncStreamReadToken token = (AsyncStreamReadToken)args.UserToken;

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
                        token.CompletionSource.SetException(new SocketException((int)SocketError.HostDown));
                    }
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int)args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(receiveBuffer, true);
            ArgsPool.Return(args);
        }

        /// <inheritdoc />
        protected override void CompleteSend(SocketAsyncEventArgs args)
        {
            AsyncStreamWriteToken token = (AsyncStreamWriteToken)args.UserToken;

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
                        token.CompletionSource.SetException(new SocketException((int)SocketError.HostDown));
                    }
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int)args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(sendBuffer, true);
            ArgsPool.Return(args);
        }

        /// <inheritdoc />
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            byte[] transmissionHeaderBuffer = BufferPool.Rent(RawStreamPacket.Header.TotalHeaderSize);
            int readHeaderBytes = 0;

            do
            {
                readHeaderBytes += Connection.Receive(transmissionHeaderBuffer, readHeaderBytes,
                    RawStreamPacket.Header.TotalHeaderSize - readHeaderBytes, flags);
            } while (readHeaderBytes < RawStreamPacket.Header.TotalHeaderSize && readHeaderBytes != 0);

            RawStreamPacket.Header header = RawStreamPacket.Header.Deserialise(transmissionHeaderBuffer);
            int totalBytes = header.DataSize;
            BufferPool.Return(transmissionHeaderBuffer, true);

            if (totalBytes > readBuffer.Length)
                throw new ArgumentException("Read buffer is too small to fully contain message.", nameof(readBuffer));

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
            if (totalBytes > MESSAGE_SIZE)
            {
                throw new ArgumentException(
                    $"Cannot receive a message of size: {totalBytes} bytes; maximum message size: {MESSAGE_SIZE} bytes",
                    nameof(readBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);

            args.SetBuffer(transmissionBuffer, 0, MESSAGE_SIZE);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncStreamReadToken token = new AsyncStreamReadToken(tcs, 0, in readBuffer);
            args.UserToken = token;

            if (Connection.ReceiveAsync(args)) return new ValueTask<int>(tcs.Task);

            CompleteReceive(args);

            return new ValueTask<int>(tcs.Task);
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            byte[] transmissionHeaderBuffer = BufferPool.Rent(RawStreamPacket.Header.TotalHeaderSize);
            new RawStreamPacket.Header(writeBuffer.Length).Serialise(transmissionHeaderBuffer);

            int writtenHeaderBytes = 0;
            do
            {
                writtenHeaderBytes += Connection.Send(transmissionHeaderBuffer, writtenHeaderBytes,
                    RawStreamPacket.Header.TotalHeaderSize - writtenHeaderBytes, flags);
            } while (writtenHeaderBytes < RawStreamPacket.Header.TotalHeaderSize && writtenHeaderBytes != 0);

            int totalBytes = writeBuffer.Length;
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
            if (totalBytes > MESSAGE_SIZE)
            {
                throw new ArgumentException(
                    $"Cannot send a message of size: {totalBytes} bytes; maximum message size: {MESSAGE_SIZE} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(totalBytes);
            writeBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer, 0, MESSAGE_SIZE);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncStreamWriteToken token = new AsyncStreamWriteToken(tcs, 0);
            args.UserToken = token;

            if (Connection.SendAsync(args)) return new ValueTask<int>(tcs.Task);

            CompleteSend(args);

            return new ValueTask<int>(tcs.Task);
        }

        private readonly struct AsyncStreamReadToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly int ExpectedBytes;
            public readonly int TotalReadBytes;
            public readonly Memory<byte> UserBuffer;

            public AsyncStreamReadToken(TaskCompletionSource<int> completionSource, int expectedBytes, int totalReadBytes, in Memory<byte> userBuffer)
            {
                CompletionSource = completionSource;

                ExpectedBytes = expectedBytes;

                TotalReadBytes = totalReadBytes;

                UserBuffer = userBuffer;
            }

            public AsyncStreamReadToken(in AsyncStreamReadToken previousToken, int newlyReadBytes)
            {
                CompletionSource = previousToken.CompletionSource;

                ExpectedBytes = previousToken.ExpectedBytes;

                TotalReadBytes = previousToken.TotalReadBytes + newlyReadBytes;

                UserBuffer = previousToken.UserBuffer;
            }
        }

        private readonly struct AsyncStreamWriteToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly int ExpectedBytes;
            public readonly int TotalWrittenBytes;

            public AsyncStreamWriteToken(TaskCompletionSource<int> completionSource, int expectedBytes, int totalWrittenBytes)
            {
                CompletionSource = completionSource;

                ExpectedBytes = expectedBytes;

                TotalWrittenBytes = totalWrittenBytes;
            }

            public AsyncStreamWriteToken(in AsyncStreamWriteToken previousToken, int newlyWrittenBytes)
            {
                CompletionSource = previousToken.CompletionSource;

                ExpectedBytes = previousToken.ExpectedBytes;

                TotalWrittenBytes = previousToken.TotalWrittenBytes + newlyWrittenBytes;
            }
        }
    }
}