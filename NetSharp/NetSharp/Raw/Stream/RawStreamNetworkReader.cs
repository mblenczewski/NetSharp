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
                    $"The message size must be greater than 0");
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

                RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in headerBuffer);

                // TODO configure the number of bytes of data to receive
                ConfigureReceiveData(args, in header);
                StartReceive(args);
            }

            void CompleteReceiveData(SocketAsyncEventArgs args)
            {
                byte[] dataBuffer = args.Buffer;

                // TODO handle request packet
                //bool haveResponsePacket = RequestHandler(args.AcceptSocket.RemoteEndPoint, );

                ConfigureSendHeader(args);
                StartSend(args);
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
            void CompleteSendHeader(SocketAsyncEventArgs args)
            {
                byte[] headerBuffer = args.Buffer;

                // TODO configure the number of bytes of data to send
                ConfigureSendData(args);
                StartSend(args);
            }

            void CompleteSendData(SocketAsyncEventArgs args)
            {
                byte[] dataBuffer = args.Buffer;

                ConfigureReceiveHeader(args);
                StartReceive(args);
            }

            bool sendingHeader = args.Buffer.Length == RawStreamPacketHeader.TotalSize;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    switch (sendingHeader)
                    {
                        case true:
                            CompleteSendHeader(args);
                            break;

                        case false:
                            CompleteSendData(args);
                            break;
                    }
                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }

        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawStreamPacketHeader receivedPacketHeader)
        {
            byte[] pendingPacketDataBuffer = BufferPool.Rent(receivedPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, pendingPacketDataBuffer.Length);
        }

        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, pendingPacketHeaderBuffer.Length);
        }

        private void ConfigureSendData(SocketAsyncEventArgs args, in RawStreamPacket pendingPacket)
        {
            byte[] pendingPacketDataBuffer = BufferPool.Rent(pendingPacket.Header.DataSize);

            pendingPacket.Data.CopyTo(pendingPacketDataBuffer);

            args.SetBuffer(pendingPacketDataBuffer, 0, pendingPacketDataBuffer.Length);
        }

        private void ConfigureSendHeader(SocketAsyncEventArgs args, in RawStreamPacket pendingPacket)
        {
            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            pendingPacket.Header.Serialise(pendingPacketHeaderBuffer);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, pendingPacketHeaderBuffer.Length);
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