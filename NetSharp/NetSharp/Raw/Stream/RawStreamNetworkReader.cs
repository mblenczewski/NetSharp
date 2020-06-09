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
        public RawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxMessageSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxMessageSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (maxMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
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
            byte[] rentedBuffer = args.Buffer;
            BufferPool.Return(rentedBuffer, true);

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

                    ConfigureReceiveHeader(args);
                    StartReceive(args);
                    break;

                default:
                    ArgsPool.Return(args);
                    break;
            }
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            void CompleteReceiveHeader(SocketAsyncEventArgs args)
            {
                Memory<byte> headerBuffer = args.Buffer;

                int receivedBytes = args.BytesTransferred,
                    previousReceivedBytes = args.Offset,
                    totalReceivedBytes = previousReceivedBytes + receivedBytes,
                    expectedBytes = args.Buffer.Length;

                if (totalReceivedBytes == expectedBytes)  // transmission complete
                {
                    RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in headerBuffer);

                    args.UserToken = header;  // allow the header to be used in the CompleteReceiveData method

                    ConfigureReceiveData(args, in header);
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

            void CompleteReceiveData(SocketAsyncEventArgs args)
            {
                Memory<byte> dataBuffer = args.Buffer;
                RawStreamPacketHeader requestPacketHeader = (RawStreamPacketHeader) args.UserToken;

                int receivedBytes = args.BytesTransferred,
                    previousReceivedBytes = args.Offset,
                    totalReceivedBytes = previousReceivedBytes + receivedBytes,
                    expectedBytes = args.Buffer.Length;

                if (totalReceivedBytes == expectedBytes)  // transmission complete
                {
                    EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                    // TODO use user-supplied delegate to get response packet size
                    int responseBufferSize = RawStreamPacket.TotalPacketSize(expectedBytes);

                    byte[] responseBuffer = BufferPool.Rent(responseBufferSize);

                    Memory<byte> responseBufferMemory = responseBuffer[RawStreamPacketHeader.TotalSize..expectedBytes];

                    // TODO rework request handler
                    bool responseExists = RequestHandler(clientEndPoint, dataBuffer, totalReceivedBytes, responseBufferMemory);

                    switch (responseExists)
                    {
                        case true:
                            RawStreamPacket response = new RawStreamPacket(in responseBufferMemory);

                            ConfigureSendResponse(args, ref responseBuffer, in response);
                            StartSend(args);
                            break;

                        case false:
                            // we manually returns the response buffer, as it wasnt set to be the args.Buffer, and since we dont have a response
                            // packet we can reuse it as a packet header buffer in the below ConfigureReceiveHeader() call
                            BufferPool.Return(responseBuffer, true);

                            ConfigureReceiveHeader(args);
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

            bool receivingHeader = args.Buffer.Length == RawStreamPacketHeader.TotalSize;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    switch (receivingHeader)
                    {
                        case true:
                            CompleteReceiveHeader(args);
                            break;

                        case false:
                            CompleteReceiveData(args);
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
            int sentBytes = args.BytesTransferred,
                previousSentBytes = args.Offset,
                totalSentBytes = previousSentBytes + sentBytes,
                expectedBytes = args.Buffer.Length;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    if (totalSentBytes == expectedBytes)  // transmission complete
                    {
                        ConfigureReceiveHeader(args);
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

        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawStreamPacketHeader receivedPacketHeader)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestHeaderBuffer (as it was already parsed)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(receivedPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, pendingPacketDataBuffer.Length);
        }

        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the responseDataBuffer (or requestDataBuffer if no response was generated)

            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, pendingPacketHeaderBuffer.Length);
        }

        private void ConfigureSendResponse(SocketAsyncEventArgs args, ref byte[] pendingPacketBuffer, in RawStreamPacket pendingPacket)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestDataBuffer (as it was already parsed)

            pendingPacket.Serialise(pendingPacketBuffer);

            args.SetBuffer(pendingPacketBuffer, 0, pendingPacketBuffer.Length);
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
    }
}