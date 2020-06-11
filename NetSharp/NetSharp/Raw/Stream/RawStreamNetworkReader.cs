using System;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw.Stream
{
    public delegate bool RawStreamRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    public sealed class RawStreamNetworkReader : RawNetworkReaderBase
    {
        private readonly RawStreamRequestHandler RequestHandler;

        /// <inheritdoc />
        public RawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxPooledMessageSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledMessageSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (maxPooledMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooledMessageSize), maxPooledMessageSize,
                    $"The maximum message size must be greater than 0");
            }

            RequestHandler = requestHandler ?? DefaultRequestHandler;
        }

        private static bool DefaultRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private void CloseClientConnection(SocketAsyncEventArgs args)
        {
            BufferPool.Return(args.Buffer, true);

            Socket clientSocket = args.AcceptSocket;

            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();

            ArgsPool.Return(args);
        }

        private void CompleteAccept(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    // the buffer is set to allow a simpler ConfigureReceiveHeader() implemetation. Since returning an empty buffer is ignored in the
                    // array pool, this allows us to just return the last assigned buffer in the ConfigureXXX() method to the pool (this means that
                    // usually we will usually be returning the ResponseDataBuffer).
                    args.SetBuffer(Array.Empty<byte>(), 0, 0);

                    ConfigureAsyncReceiveHeader(args);
                    StartReceive(args);
                    break;

                default:
                    ArgsPool.Return(args);
                    break;
            }
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            PacketReadToken readToken = (PacketReadToken) args.UserToken;

            void CompleteReceiveHeader(SocketAsyncEventArgs args, in PacketReadToken readToken)
            {
                Memory<byte> headerBuffer = args.Buffer;

                int receivedBytes = args.BytesTransferred,
                    previousReceivedBytes = args.Offset,
                    totalReceivedBytes = previousReceivedBytes + receivedBytes,
                    expectedBytes = readToken.BytesToTransfer;

                if (totalReceivedBytes == expectedBytes)  // transmission complete
                {
                    RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in headerBuffer);

                    ConfigureAsyncReceiveData(args, in header);
                    StartReceive(args);
                }
                else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
                {
                    args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                    ContinueReceive(args);
                }
                else if (receivedBytes == 0)  // connection is dead
                {
                    CloseClientConnection(args);
                }
            }

            void CompleteReceiveData(SocketAsyncEventArgs args, in PacketReadToken readToken)
            {
                Memory<byte> dataBuffer = args.Buffer;

                int receivedBytes = args.BytesTransferred,
                    previousReceivedBytes = args.Offset,
                    totalReceivedBytes = previousReceivedBytes + receivedBytes,
                    expectedBytes = readToken.BytesToTransfer;

                if (totalReceivedBytes == expectedBytes)  // transmission complete
                {
                    EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                    // TODO use user-supplied delegate to generate response packet header
                    RawStreamPacketHeader responseHeader = new RawStreamPacketHeader(expectedBytes);
                    int responseBufferSize = RawStreamPacket.TotalPacketSize(in responseHeader);

                    byte[] responseBuffer = BufferPool.Rent(responseBufferSize);

                    Memory<byte> responseBufferMemory = responseBuffer.AsMemory(RawStreamPacketHeader.TotalSize, responseHeader.DataSize);

                    // TODO rework request handler
                    bool responseExists = RequestHandler(clientEndPoint, dataBuffer, totalReceivedBytes, responseBufferMemory);

                    switch (responseExists)
                    {
                        case true:
                            ConfigureAsyncSendPacket(args, ref responseBuffer, in responseHeader, responseBufferMemory);
                            StartSend(args);
                            break;

                        case false:
                            // we manually returns the response buffer, as it wasnt set to be the args.Buffer, and since we dont have a response
                            // packet we can reuse it as a packet header buffer in the below ConfigureReceiveHeader() call
                            BufferPool.Return(responseBuffer, true);

                            ConfigureAsyncReceiveHeader(args);
                            StartReceive(args);
                            break;
                    }
                }
                else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
                {
                    args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                    ContinueReceive(args);
                }
                else if (receivedBytes == 0)  // connection is dead
                {
                    CloseClientConnection(args);
                }
            }

            bool receivingHeader = readToken.BytesToTransfer == RawStreamPacketHeader.TotalSize;

            switch (args.SocketError)
            {
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
                    CloseClientConnection(args);
                    break;
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
                case SocketError.Success:
                    if (totalSentBytes == expectedBytes)  // transmission complete
                    {
                        ConfigureAsyncReceiveHeader(args);
                        StartReceive(args);
                    }
                    else if (0 < totalSentBytes && totalSentBytes < expectedBytes)  // transmission not complete
                    {
                        args.SetBuffer(totalSentBytes, expectedBytes - totalSentBytes);

                        ContinueSend(args);
                    }
                    else if (sentBytes == 0)  // connection is dead
                    {
                        CloseClientConnection(args);
                    }
                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }

        private void ConfigureAsyncReceiveData(SocketAsyncEventArgs args, in RawStreamPacketHeader receivedPacketHeader)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestHeaderBuffer (as it was already parsed)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(receivedPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, receivedPacketHeader.DataSize);
            args.UserToken = new PacketReadToken(receivedPacketHeader.DataSize, receivedPacketHeader);
        }

        private void ConfigureAsyncReceiveHeader(SocketAsyncEventArgs args)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the responseDataBuffer (or requestDataBuffer if no response was generated)

            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, RawStreamPacketHeader.TotalSize);
            args.UserToken = new PacketReadToken(RawStreamPacketHeader.TotalSize, null);
        }

        private void ConfigureAsyncSendPacket(SocketAsyncEventArgs args, ref byte[] pendingPacketBuffer, in RawStreamPacketHeader pendingPacketHeader,
            in ReadOnlyMemory<byte> pendingPacketData)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestDataBuffer (as it was already parsed)

            RawStreamPacket.Serialise(pendingPacketBuffer, in pendingPacketHeader, in pendingPacketData);

            int totalPacketSize = RawStreamPacket.TotalPacketSize(in pendingPacketHeader);
            args.SetBuffer(pendingPacketBuffer, 0, totalPacketSize);
            args.UserToken = new PacketWriteToken(totalPacketSize);
        }

        private void ContinueReceive(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.ReceiveAsync(args))
            {
                return;
            }

            CompleteReceive(args);
        }

        private void ContinueSend(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.SendAsync(args))
            {
                return;
            }

            CompleteSend(args);
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    StartDefaultAccept();
                    CompleteAccept(args);
                    break;

                case SocketAsyncOperation.Send:
                    CompleteSend(args);
                    break;

                case SocketAsyncOperation.Receive:
                    CompleteReceive(args);
                    break;
            }
        }

        private void StartAccept(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                return;
            }

            if (Connection.AcceptAsync(args))
            {
                return;
            }

            StartDefaultAccept();
            CompleteAccept(args);
        }

        private void StartDefaultAccept()
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                return;
            }

            SocketAsyncEventArgs args = ArgsPool.Rent();
            StartAccept(args);
        }

        private void StartReceive(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.ReceiveAsync(args))
            {
                return;
            }

            CompleteReceive(args);
        }

        private void StartSend(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.SendAsync(args))
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
            instance.SetBuffer(null, 0, 0);

            instance.AcceptSocket = null;
        }

        /// <inheritdoc />
        public override void Start(ushort concurrentReadTasks)
        {
            for (ushort i = 0; i < concurrentReadTasks; i++)
            {
                StartDefaultAccept();
            }
        }

        private readonly struct PacketReadToken
        {
            public readonly int BytesToTransfer;
            public readonly RawStreamPacketHeader? Header;

            public PacketReadToken(int bytesToTransfer, in RawStreamPacketHeader? header)
            {
                BytesToTransfer = bytesToTransfer;

                Header = header;
            }
        }

        private readonly struct PacketWriteToken
        {
            public readonly int BytesToTransfer;

            public PacketWriteToken(int bytesToTransfer)
            {
                BytesToTransfer = bytesToTransfer;
            }
        }
    }
}