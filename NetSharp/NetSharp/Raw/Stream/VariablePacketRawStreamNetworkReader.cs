namespace NetSharp.Raw.Stream
{
    /*
    public sealed class VariablePacketRawStreamNetworkReader : RawStreamNetworkReader
    {
        /// <inheritdoc />
        public VariablePacketRawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxMessageSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, requestHandler, defaultEndPoint, maxMessageSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawStreamPacket.Header header)
        {
            byte[] receiveBuffer = BufferPool.Rent(header.DataSize);
            args.SetBuffer(receiveBuffer, 0, header.DataSize);

            TransmissionToken token = new TransmissionToken(header.DataSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(RawStreamPacket.Header.TotalHeaderSize);
            args.SetBuffer(receiveBuffer, 0, RawStreamPacket.Header.TotalHeaderSize);

            TransmissionToken token = new TransmissionToken(RawStreamPacket.Header.TotalHeaderSize, 0);
            args.UserToken = token;
        }

        /// <inheritdoc />
        protected override void CompleteAccept(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    ConfigureReceiveHeader(args);

                    StartReceive(args);
                    break;

                default:
                    ArgsPool.Return(args);
                    break;
            }

            StartDefaultAccept();
        }

        /// <inheritdoc />
        protected override void CompleteReceive(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken) args.UserToken;

            byte[] receiveBuffer = args.Buffer;
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            int expectedBytes = token.ExpectedBytes;

            bool readHeader = expectedBytes == RawStreamPacket.Header.TotalHeaderSize;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int receivedBytes = args.BytesTransferred, previousReceivedBytes = token.BytesTransferred, totalReceivedBytes = previousReceivedBytes + receivedBytes;

                    if (totalReceivedBytes == expectedBytes)  // transmission complete
                    {
                        if (readHeader)  // handle a received message header
                        {
                            Memory<byte> headerBuffer = receiveBufferMemory.Slice(0, RawStreamPacket.Header.TotalHeaderSize);
                            RawStreamPacket.Header header = RawStreamPacket.Header.Deserialise(in headerBuffer);

                            ConfigureReceiveData(args, in header);

                            StartReceive(args);
                        }
                        else  // handle a received message header
                        {
                            EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                            int responseBufferSize = RawStreamPacket.Header.TotalHeaderSize + expectedBytes;
                            byte[] responseBuffer = BufferPool.Rent(responseBufferSize);
                            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                            Memory<byte> headerMemory = responseBufferMemory.Slice(0, RawStreamPacket.Header.TotalHeaderSize);
                            Memory<byte> responseMemory = responseBufferMemory.Slice(RawStreamPacket.Header.TotalHeaderSize, expectedBytes);

                            bool responseExists = RequestHandler(clientEndPoint, receiveBuffer[..expectedBytes], totalReceivedBytes, responseMemory);
                            BufferPool.Return(receiveBuffer, true);

                            if (responseExists)
                            {
                                RawStreamPacket.Header responseHeader = new RawStreamPacket.Header(responseMemory.Length);
                                responseHeader.Serialise(in headerMemory);

                                args.SetBuffer(responseBuffer, 0, responseBufferSize);

                                TransmissionToken sendToken = new TransmissionToken(responseBufferSize, 0);
                                args.UserToken = sendToken;

                                StartSend(args);
                                return;
                            }

                            BufferPool.Return(responseBuffer, true);

                            ConfigureReceiveHeader(args);

                            StartReceive(args);
                        }
                    }
                    else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
                    {
                        token = new TransmissionToken(in token, receivedBytes);
                        args.UserToken = token;

                        args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                        ContinueReceive(args);
                    }
                    else if (receivedBytes == 0)  // connection is dead
                    {
                        CloseClientConnection(args);
                    }
                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }

        /// <inheritdoc />
        protected override void CompleteSend(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken) args.UserToken;

            byte[] sendBuffer = args.Buffer;
            int expectedBytes = token.ExpectedBytes;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int sentBytes = args.BytesTransferred, previousSentBytes = token.BytesTransferred, totalSentBytes = previousSentBytes + sentBytes;

                    if (totalSentBytes == expectedBytes)  // transmission complete
                    {
                        BufferPool.Return(sendBuffer, true);

                        ConfigureReceiveHeader(args);

                        StartReceive(args);
                    }
                    else if (0 < totalSentBytes && totalSentBytes < expectedBytes)  // transmission not complete
                    {
                        token = new TransmissionToken(in token, sentBytes);
                        args.UserToken = token;

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
    }
    */
}