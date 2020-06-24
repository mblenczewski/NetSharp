using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Packets;

namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Implements a raw network writer using a stream-based protocol.
    /// </summary>
    public sealed class RawStreamNetworkWriter : RawNetworkWriterBase
    {
        /// <inheritdoc />
        public RawStreamNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledMessageSize = DefaultMaxPooledBufferSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledMessageSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (maxPooledMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooledMessageSize), maxPooledMessageSize, Properties.Resources.RawStreamMessageSizeUnderflow);
            }
        }

        private static void ConfigureAsyncSendPacket(SocketAsyncEventArgs args, ref byte[] pendingPacketBuffer, in RawStreamPacketHeader pendingPacketHeader,
            in ReadOnlyMemory<byte> userDataBuffer, TaskCompletionSource<int> tcs)
        {
            RawStreamPacket.Serialise(pendingPacketBuffer, in pendingPacketHeader, in userDataBuffer);

            int totalPacketSize = RawStreamPacket.TotalPacketSize(in pendingPacketHeader);
            args.SetBuffer(pendingPacketBuffer, 0, totalPacketSize);
            args.UserToken = new PacketWriteToken(totalPacketSize, tcs);
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            PacketReadToken readToken = (PacketReadToken) args.UserToken;

            bool receivingHeader = readToken.BytesToTransfer == RawStreamPacketHeader.TotalSize;

            switch (args.SocketError)
            {
                case SocketError.OperationAborted:
                    readToken.CompletionSource.SetCanceled();

                    CleanupTransmissionBufferAndState(args);
                    break;

                case SocketError.Success:
                    switch (receivingHeader)
                    {
                        case true:
                            CompleteReceiveHeader(args, in readToken);
                            break;

                        case false:
                            CompleteReceiveData(args, in readToken);
                            break;
                    }
                    break;

                default:
                    readToken.CompletionSource.SetException(new SocketException((int) args.SocketError));

                    CleanupTransmissionBufferAndState(args);
                    break;
            }
        }

        private void CompleteReceiveData(SocketAsyncEventArgs args, in PacketReadToken readToken)
        {
            int receivedBytes = args.BytesTransferred,
                previousReceivedBytes = args.Offset,
                totalReceivedBytes = previousReceivedBytes + receivedBytes,
                expectedBytes = readToken.BytesToTransfer;

            if (totalReceivedBytes == expectedBytes)  // transmission complete
            {
                args.Buffer.AsMemory(0, readToken.UserDataBuffer.Length).CopyTo(readToken.UserDataBuffer);

                // we only return the number of bytes of user data that were read
                readToken.CompletionSource.SetResult(totalReceivedBytes);

                CleanupTransmissionBufferAndState(args);
            }
            else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
            {
                args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                ContinueReceive(args);
            }
            else if (receivedBytes == 0)  // connection is dead
            {
                readToken.CompletionSource.SetException(new SocketException((int) SocketError.HostDown));

                CleanupTransmissionBufferAndState(args);
            }
        }

        private void CompleteReceiveHeader(SocketAsyncEventArgs args, in PacketReadToken readToken)
        {
            int receivedBytes = args.BytesTransferred,
                previousReceivedBytes = args.Offset,
                totalReceivedBytes = previousReceivedBytes + receivedBytes,
                expectedBytes = readToken.BytesToTransfer;

            if (totalReceivedBytes == expectedBytes)  // transmission complete
            {
                Memory<byte> headerBuffer = args.Buffer.AsMemory(0, RawStreamPacketHeader.TotalSize);
                RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in headerBuffer);

                ConfigureAsyncReceiveData(args, in header, in readToken.UserDataBuffer, readToken.CompletionSource);

                StartReceive(args);
            }
            else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
            {
                args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                ContinueReceive(args);
            }
            else if (receivedBytes == 0)  // connection is dead
            {
                readToken.CompletionSource.SetException(new SocketException((int) SocketError.HostDown));

                CleanupTransmissionBufferAndState(args);
            }
        }

        private void CompleteSend(SocketAsyncEventArgs args)
        {
            PacketWriteToken writeToken = (PacketWriteToken) args.UserToken;

            int sentBytes = args.BytesTransferred,
                previousSentBytes = args.Offset,
                totalSentBytes = previousSentBytes + sentBytes,
                expectedBytes = writeToken.BytesToTransfer;

            switch (args.SocketError)
            {
                case SocketError.OperationAborted:
                    writeToken.CompletionSource.SetCanceled();

                    CleanupTransmissionBufferAndState(args);
                    break;

                case SocketError.Success:
                    if (totalSentBytes == expectedBytes) // transmission complete
                    {
                        // we only return the number of bytes of user data that were written
                        writeToken.CompletionSource.SetResult(totalSentBytes - RawStreamPacketHeader.TotalSize);

                        CleanupTransmissionBufferAndState(args);
                    }
                    else if (0 < totalSentBytes && totalSentBytes < expectedBytes)  // transmission not complete
                    {
                        args.SetBuffer(totalSentBytes, expectedBytes - totalSentBytes);

                        ContinueSend(args);
                    }
                    else if (sentBytes == 0)  // connection is dead
                    {
                        writeToken.CompletionSource.SetException(new SocketException((int) SocketError.HostDown));

                        CleanupTransmissionBufferAndState(args);
                    }
                    break;

                default:
                    writeToken.CompletionSource.SetException(new SocketException((int) args.SocketError));

                    CleanupTransmissionBufferAndState(args);
                    break;
            }
        }

        private void ConfigureAsyncReceiveData(SocketAsyncEventArgs args, in RawStreamPacketHeader receivedPacketHeader, in Memory<byte> userDataBuffer,
            TaskCompletionSource<int> tcs)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestHeaderBuffer (as it was already parsed)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(receivedPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, receivedPacketHeader.DataSize);

            // TODO add transmission state token
            args.UserToken = new PacketReadToken(receivedPacketHeader.DataSize, receivedPacketHeader, in userDataBuffer, tcs);
        }

        private void ConfigureAsyncReceiveHeader(SocketAsyncEventArgs args, in Memory<byte> userDataBuffer, TaskCompletionSource<int> tcs)
        {
            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, RawStreamPacketHeader.TotalSize);
            args.UserToken = new PacketReadToken(RawStreamPacketHeader.TotalSize, null, in userDataBuffer, tcs);
        }

        private void ContinueReceive(SocketAsyncEventArgs args)
        {
            if (Connection.ReceiveAsync(args))
            {
                return;
            }

            CompleteReceive(args);
        }

        private void ContinueSend(SocketAsyncEventArgs args)
        {
            if (Connection.SendAsync(args))
            {
                return;
            }

            CompleteSend(args);
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    CompleteReceive(args);
                    break;

                case SocketAsyncOperation.Send:
                    CompleteSend(args);
                    break;
            }
        }

        private void StartReceive(SocketAsyncEventArgs args)
        {
            if (Connection.ReceiveAsync(args))
            {
                return;
            }

            CompleteReceive(args);
        }

        private void StartSend(SocketAsyncEventArgs args)
        {
            if (Connection.SendAsync(args))
            {
                return;
            }

            CompleteSend(args);
        }

        /// <inheritdoc />
        protected override bool CanReuseStateObject(ref SocketAsyncEventArgs instance)
        {
            return true;
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateStateObject()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += HandleIoCompleted;

            return args;
        }

        /// <inheritdoc />
        protected override void DestroyStateObject(SocketAsyncEventArgs instance)
        {
            instance.Completed -= HandleIoCompleted;
            instance.Dispose();
        }

        /// <inheritdoc />
        protected override void ResetStateObject(ref SocketAsyncEventArgs instance)
        {
        }

        /// <inheritdoc />
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            static int ReadBytesIntoBuffer(Socket connection, ref byte[] buffer, int count, SocketFlags flags)
            {
                int readBytes = 0;

                do
                {
                    readBytes += connection.Receive(buffer, readBytes, count - readBytes, flags);
                } while (readBytes < count && readBytes > 0);

                return readBytes;
            }

            byte[] pendingHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            int _ = ReadBytesIntoBuffer(Connection, ref pendingHeaderBuffer, RawStreamPacketHeader.TotalSize, flags);

            RawStreamPacketHeader packetHeader = RawStreamPacketHeader.Deserialise(pendingHeaderBuffer);
            BufferPool.Return(pendingHeaderBuffer, true);  // return and clear the pendingHeaderBuffer (as it was already parsed)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(packetHeader.DataSize);

            int bodyBytes = ReadBytesIntoBuffer(Connection, ref pendingPacketDataBuffer, packetHeader.DataSize, flags);

            pendingPacketDataBuffer.AsMemory(0, readBuffer.Length).CopyTo(readBuffer);
            BufferPool.Return(pendingPacketDataBuffer, true);  // return and clear the pendingDataBuffer (as it was already copied)

            return bodyBytes;  // we only return the number of bytes of user data that were read
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            ConfigureAsyncReceiveHeader(args, in readBuffer, tcs);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            StartReceive(args);

            return new ValueTask<int>(tcs.Task);
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            static int WriteBytesFromBuffer(Socket connection, ref byte[] buffer, int count, SocketFlags flags)
            {
                int writtenBytes = 0;

                do
                {
                    writtenBytes += connection.Send(buffer, writtenBytes, count - writtenBytes, flags);
                } while (writtenBytes < count && writtenBytes > 0);

                return writtenBytes;
            }

            RawStreamPacketHeader pendingPacketHeader = new RawStreamPacketHeader(writeBuffer.Length);
            int totalPacketSize = RawStreamPacket.TotalPacketSize(in pendingPacketHeader);
            byte[] pendingPacketBuffer = BufferPool.Rent(totalPacketSize);

            RawStreamPacket.Serialise(pendingPacketBuffer, in pendingPacketHeader, in writeBuffer);

            int _ = WriteBytesFromBuffer(Connection, ref pendingPacketBuffer, totalPacketSize, flags);
            BufferPool.Return(pendingPacketBuffer, true);  // return and clear the pendingPacketBuffer (as it was already cleared)

            return pendingPacketHeader.DataSize;  // we only return the number of bytes of user data that were written
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            RawStreamPacketHeader pendingPacketHeader = new RawStreamPacketHeader(writeBuffer.Length);
            int totalPacketSize = RawStreamPacket.TotalPacketSize(in pendingPacketHeader);
            byte[] pendingPacketBuffer = BufferPool.Rent(totalPacketSize);

            ConfigureAsyncSendPacket(args, ref pendingPacketBuffer, in pendingPacketHeader, in writeBuffer, tcs);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            StartSend(args);

            return new ValueTask<int>(tcs.Task);
        }

        private readonly struct PacketReadToken
        {
            internal readonly int BytesToTransfer;
            internal readonly TaskCompletionSource<int> CompletionSource;
            internal readonly RawStreamPacketHeader? Header;
            internal readonly Memory<byte> UserDataBuffer;

            internal PacketReadToken(int bytesToTransfer, in RawStreamPacketHeader? header, in Memory<byte> userDataBuffer, TaskCompletionSource<int> tcs)
            {
                BytesToTransfer = bytesToTransfer;

                Header = header;

                UserDataBuffer = userDataBuffer;

                CompletionSource = tcs;
            }
        }

        private readonly struct PacketWriteToken
        {
            internal readonly int BytesToTransfer;
            internal readonly TaskCompletionSource<int> CompletionSource;

            internal PacketWriteToken(int bytesToTransfer, TaskCompletionSource<int> tcs)
            {
                BytesToTransfer = bytesToTransfer;

                CompletionSource = tcs;
            }
        }
    }
}