using System;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Represents a method that handles a request received by a <see cref="RawStreamNetworkReader" />.
    /// </summary>
    /// <param name="remoteEndPoint">
    /// The remote endpoint from which the request was received.
    /// </param>
    /// <param name="requestBuffer">
    /// The buffer containing the received request.
    /// </param>
    /// <param name="receivedRequestBytes">
    /// The number of bytes of user data received in the request.
    /// </param>
    /// <param name="responseBuffer">
    /// The buffer into which the response should be written.
    /// </param>
    /// <returns>
    /// Whether there exists a response to be sent back to the remote endpoint.
    /// </returns>
    // TODO implement this in a better, more robust and extensible way
    public delegate bool RawStreamRequestHandler(
        EndPoint remoteEndPoint,
        in ReadOnlyMemory<byte> requestBuffer,
        int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    /// <summary>
    /// Implements a raw network reader using a stream-based protocol.
    /// </summary>
    public sealed class RawStreamNetworkReader : RawNetworkReaderBase
    {
        private readonly RawStreamRequestHandler requestHandler;

        /// <inheritdoc cref="RawNetworkReaderBase(ref Socket, EndPoint, int, int, uint)"/>
        public RawStreamNetworkReader(
            ref Socket rawConnection,
            RawStreamRequestHandler? requestHandler,
            EndPoint defaultEndPoint,
            int maxPooledMessageSize,
            int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0)
            : base(ref rawConnection, defaultEndPoint, maxPooledMessageSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (maxPooledMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooledMessageSize), maxPooledMessageSize, Properties.Resources.RawStreamMessageSizeUnderflow);
            }

            this.requestHandler = requestHandler ?? DefaultRequestHandler;
        }

        /// <inheritdoc />
        public override void Start(ushort concurrentReadTasks)
        {
            for (ushort i = 0; i < concurrentReadTasks; i++)
            {
                StartDefaultAccept();
            }
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

        private static bool DefaultRequestHandler(
            EndPoint remoteEndPoint,
            in ReadOnlyMemory<byte> requestBuffer,
            int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private void CloseClientConnection(SocketAsyncEventArgs args)
        {
            Socket serversideClient = args.AcceptSocket;

            serversideClient.Disconnect(false);
            serversideClient.Shutdown(SocketShutdown.Both);
            serversideClient.Close();
            serversideClient.Dispose();

            CleanupTransmissionBufferAndState(args);
        }

        private void CompleteAccept(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    // ensure that the serverside client socket will be successfully, gracefully shutdown after the conection ends
                    args.AcceptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

                    // the buffer is set to allow a simpler ConfigureReceiveHeader() implemetation. Since returning an empty buffer is ignored in the
                    // array pool, this allows us to just return the last assigned buffer in the ConfigureXXX() method to the pool (this means that
                    // usually we will usually be returning the ResponseDataBuffer).
                    args.SetBuffer(Array.Empty<byte>(), 0, 0);

                    ConfigureAsyncReceiveHeader(args);
                    StartOrContinueReceive(args);
                    break;

                default:
                    CleanupTransmissionBufferAndState(args);
                    break;
            }
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            PacketReadToken readToken = (PacketReadToken)args.UserToken;

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

        private void CompleteReceiveData(SocketAsyncEventArgs args, in PacketReadToken readToken)
        {
            Memory<byte> dataBuffer = args.Buffer;

            int receivedBytes = args.BytesTransferred,
                previousReceivedBytes = args.Offset,
                totalReceivedBytes = previousReceivedBytes + receivedBytes,
                expectedBytes = readToken.BytesToTransfer;

            if (totalReceivedBytes == expectedBytes)
            {
                // transmission complete
                EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                // TODO use user-supplied delegate to generate response packet header
                RawStreamPacketHeader responseHeader = new RawStreamPacketHeader(expectedBytes);
                int responseBufferSize = RawStreamPacket.TotalPacketSize(in responseHeader);

                byte[] responseBuffer = BufferPool.Rent(responseBufferSize);

                Memory<byte> responseBufferMemory = responseBuffer.AsMemory(RawStreamPacketHeader.TotalSize, responseHeader.DataSize);

                // TODO rework request handler
                bool responseExists = requestHandler(clientEndPoint, dataBuffer, totalReceivedBytes, responseBufferMemory);

                switch (responseExists)
                {
                    case true:
                        ConfigureAsyncSendPacket(args, ref responseBuffer, in responseHeader, responseBufferMemory);
                        StartOrContinueSend(args);
                        break;

                    case false:
                        // we manually returns the response buffer, as it wasnt set to be the args.Buffer, and since we dont have a response packet we
                        // can reuse it as a packet header buffer in the below ConfigureAsyncReceiveHeader() call
                        BufferPool.Return(responseBuffer, true);

                        ConfigureAsyncReceiveHeader(args);
                        StartOrContinueReceive(args);
                        break;
                }
            }
            else if (totalReceivedBytes > 0 && totalReceivedBytes < expectedBytes)
            {
                // transmission not complete
                args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                StartOrContinueReceive(args);
            }
            else if (receivedBytes == 0)
            {
                // connection is dead
                CloseClientConnection(args);
            }
        }

        private void CompleteReceiveHeader(SocketAsyncEventArgs args, in PacketReadToken readToken)
        {
            Memory<byte> headerBuffer = args.Buffer;

            int receivedBytes = args.BytesTransferred,
                previousReceivedBytes = args.Offset,
                totalReceivedBytes = previousReceivedBytes + receivedBytes,
                expectedBytes = readToken.BytesToTransfer;

            if (totalReceivedBytes == expectedBytes)
            {
                // transmission complete
                RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in headerBuffer);

                ConfigureAsyncReceiveData(args, in header);
                StartOrContinueReceive(args);
            }
            else if (totalReceivedBytes > 0 && totalReceivedBytes < expectedBytes)
            {
                // transmission not complete
                args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                StartOrContinueReceive(args);
            }
            else if (receivedBytes == 0)
            {
                // connection is dead
                CloseClientConnection(args);
            }
        }

        private void CompleteSend(SocketAsyncEventArgs args)
        {
            PacketWriteToken writeToken = (PacketWriteToken)args.UserToken;

            int sentBytes = args.BytesTransferred,
                previousSentBytes = args.Offset,
                totalSentBytes = previousSentBytes + sentBytes,
                expectedBytes = writeToken.BytesToTransfer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    if (totalSentBytes == expectedBytes)
                    {
                        // transmission complete
                        ConfigureAsyncReceiveHeader(args);
                        StartOrContinueReceive(args);
                    }
                    else if (totalSentBytes > 0 && totalSentBytes < expectedBytes)
                    {
                        // transmission not complete
                        args.SetBuffer(totalSentBytes, expectedBytes - totalSentBytes);

                        StartOrContinueSend(args);
                    }
                    else if (sentBytes == 0)
                    {
                        // connection is dead
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

        private void ConfigureAsyncSendPacket(
            SocketAsyncEventArgs args,
            ref byte[] pendingPacketBuffer,
            in RawStreamPacketHeader pendingPacketHeader,
            in ReadOnlyMemory<byte> pendingPacketData)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestDataBuffer (as it was already parsed)

            RawStreamPacket.Serialise(pendingPacketBuffer, in pendingPacketHeader, in pendingPacketData);

            int totalPacketSize = RawStreamPacket.TotalPacketSize(in pendingPacketHeader);
            args.SetBuffer(pendingPacketBuffer, 0, totalPacketSize);
            args.UserToken = new PacketWriteToken(totalPacketSize);
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
            if (Connection.AcceptAsync(args))
            {
                return;
            }

            StartDefaultAccept();
            CompleteAccept(args);
        }

        private void StartDefaultAccept()
        {
            SocketAsyncEventArgs args = ArgsPool.Rent();

            StartAccept(args);
        }

        private void StartOrContinueReceive(SocketAsyncEventArgs args)
        {
            Socket serversideClient = args.AcceptSocket;

            if (serversideClient.ReceiveAsync(args))
            {
                return;
            }

            CompleteReceive(args);
        }

        private void StartOrContinueSend(SocketAsyncEventArgs args)
        {
            Socket serversideClient = args.AcceptSocket;

            if (serversideClient.SendAsync(args))
            {
                return;
            }

            CompleteSend(args);
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
