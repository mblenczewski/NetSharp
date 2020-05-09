using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using NetSharp.Utils.Conversion;

namespace NetSharp.Raw.Stream
{
    public delegate bool RawStreamRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    public readonly struct RawMessage
    {
        public readonly Memory<byte> MsgData;
        public readonly Header MsgHeader;

        private RawMessage(in Header header, in Memory<byte> data)
        {
            MsgHeader = header;

            MsgData = data;
        }

        public RawMessage(in Memory<byte> data)
        {
            MsgHeader = new Header(data.Length);

            MsgData = data;
        }

        public static RawMessage Deserialise(in Memory<byte> buffer)
        {
            Memory<byte> serialisedHeader = buffer.Slice(0, Header.TotalHeaderSize);
            Header header = Header.Deserialise(in serialisedHeader);

            Memory<byte> serialisedData = buffer.Slice(Header.TotalHeaderSize);

            return new RawMessage(in header, in serialisedData);
        }

        public void Serialise(in Memory<byte> buffer)
        {
            MsgHeader.Serialise(buffer.Slice(0, Header.TotalHeaderSize));

            MsgData.CopyTo(buffer.Slice(Header.TotalHeaderSize, MsgData.Length));
        }

        public readonly struct Header
        {
            public const int TotalHeaderSize = sizeof(int);

            public readonly int DataSize;

            internal Header(int dataSize)
            {
                DataSize = dataSize;
            }

            public static Header Deserialise(in Memory<byte> buffer)
            {
                Span<byte> serialisedDataSize = buffer.Slice(0, sizeof(int)).Span;
                int dataSize = EndianAwareBitConverter.ToInt32(serialisedDataSize);

                return new Header(dataSize);
            }

            public void Serialise(in Memory<byte> buffer)
            {
                Span<byte> serialisedDataSize = EndianAwareBitConverter.GetBytes(DataSize);
                serialisedDataSize.CopyTo(buffer.Slice(0, sizeof(int)).Span);
            }
        }
    }

    public sealed class RawStreamNetworkReader : RawNetworkReaderBase
    {
        private readonly RawStreamRequestHandler requestHandler;

        /// <inheritdoc />
        public RawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxMessageSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxMessageSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            this.requestHandler = requestHandler ?? DefaultRequestHandler;
        }

        private static bool DefaultRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private void CloseClientConnection(SocketAsyncEventArgs args)
        {
            args.BufferList = null;

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
                    //ConfigureReceiveHeader(args);  // inlined for performance
                    byte[] receiveBuffer = BufferPool.Rent(RawMessage.Header.TotalHeaderSize);
                    args.SetBuffer(receiveBuffer, 0, RawMessage.Header.TotalHeaderSize);

                    args.UserToken = new TransmissionToken(RawMessage.Header.TotalHeaderSize, 0);

                    StartReceive(args);
                    break;

                default:
                    ArgsPool.Return(args);
                    break;
            }

            StartDefaultAccept();
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken)args.UserToken;

            byte[] receiveBuffer = args.Buffer;
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            int expectedBytes = token.ExpectedBytes;

            bool readHeader = expectedBytes == RawMessage.Header.TotalHeaderSize;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int receivedBytes = args.BytesTransferred, previousReceivedBytes = token.BytesTransferred, totalReceivedBytes = previousReceivedBytes + receivedBytes;

                    if (totalReceivedBytes == expectedBytes)  // transmission complete
                    {
                        if (readHeader)  // handle a received message header
                        {
                            Memory<byte> headerBuffer = receiveBufferMemory.Slice(0, RawMessage.Header.TotalHeaderSize);
                            RawMessage.Header header = RawMessage.Header.Deserialise(in headerBuffer);

                            //ConfigureReceiveData(args, in header);  // inlined for performance
                            byte[] newReceiveBuffer = BufferPool.Rent(header.DataSize);
                            args.SetBuffer(newReceiveBuffer, 0, header.DataSize);

                            token = new TransmissionToken(header.DataSize, 0);
                            args.UserToken = token;

                            StartReceive(args);
                        }
                        else  // handle a received message header
                        {
                            EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                            int responseBufferSize = RawMessage.Header.TotalHeaderSize + expectedBytes;
                            byte[] responseBuffer = BufferPool.Rent(responseBufferSize);
                            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                            Memory<byte> headerMemory = responseBufferMemory.Slice(0, RawMessage.Header.TotalHeaderSize);
                            Memory<byte> responseMemory = responseBufferMemory.Slice(RawMessage.Header.TotalHeaderSize, expectedBytes);

                            bool responseExists = requestHandler(clientEndPoint, receiveBuffer[..expectedBytes], totalReceivedBytes, responseMemory);
                            BufferPool.Return(receiveBuffer, true);

                            if (responseExists)
                            {
                                RawMessage.Header responseHeader = new RawMessage.Header(responseMemory.Length);
                                responseHeader.Serialise(in headerMemory);

                                args.SetBuffer(responseBuffer, 0, responseBufferSize);

                                TransmissionToken sendToken = new TransmissionToken(responseBufferSize, 0);
                                args.UserToken = sendToken;

                                StartSend(args);
                                return;
                            }

                            BufferPool.Return(responseBuffer, true);

                            //ConfigureReceiveHeader(args);  // inlined for performance
                            byte[] newReceiveBuffer = BufferPool.Rent(RawMessage.Header.TotalHeaderSize);
                            args.SetBuffer(newReceiveBuffer, 0, RawMessage.Header.TotalHeaderSize);

                            args.UserToken = new TransmissionToken(RawMessage.Header.TotalHeaderSize, 0);

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

        private void CompleteSend(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken)args.UserToken;

            byte[] sendBuffer = args.Buffer;
            int expectedBytes = token.ExpectedBytes;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int sentBytes = args.BytesTransferred, previousSentBytes = token.BytesTransferred, totalSentBytes = previousSentBytes + sentBytes;

                    if (totalSentBytes == expectedBytes)  // transmission complete
                    {
                        BufferPool.Return(sendBuffer, true);

                        //ConfigureReceiveHeader(args);  // inlined for performance
                        byte[] newReceiveBuffer = BufferPool.Rent(RawMessage.Header.TotalHeaderSize);
                        args.SetBuffer(newReceiveBuffer, 0, RawMessage.Header.TotalHeaderSize);

                        args.UserToken = new TransmissionToken(RawMessage.Header.TotalHeaderSize, 0);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawMessage.Header header)
        {
            byte[] receiveBuffer = BufferPool.Rent(header.DataSize);
            args.SetBuffer(receiveBuffer, 0, header.DataSize);

            TransmissionToken token = new TransmissionToken(header.DataSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(RawMessage.Header.TotalHeaderSize);
            args.SetBuffer(receiveBuffer, 0, RawMessage.Header.TotalHeaderSize);

            TransmissionToken token = new TransmissionToken(RawMessage.Header.TotalHeaderSize, 0);
            args.UserToken = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureSend(SocketAsyncEventArgs args)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ContinueReceive(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.ReceiveAsync(args)) return;

            CompleteReceive(args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ContinueSend(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CloseClientConnection(args);
                return;
            }

            Socket clientSocket = args.AcceptSocket;

            if (clientSocket.SendAsync(args)) return;

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

            if (Connection.AcceptAsync(args)) return;

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

            if (clientSocket.ReceiveAsync(args)) return;

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

            if (clientSocket.SendAsync(args)) return;

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

        private readonly struct TransmissionToken
        {
            public readonly int BytesTransferred;
            public readonly int ExpectedBytes;

            public TransmissionToken(int expectedBytes, int bytesTransferred)
            {
                ExpectedBytes = expectedBytes;

                BytesTransferred = bytesTransferred;
            }

            public TransmissionToken(in TransmissionToken token, int newlyTransferredBytes)
            {
                ExpectedBytes = token.ExpectedBytes;

                BytesTransferred = token.BytesTransferred + newlyTransferredBytes;
            }
        }
    }
}