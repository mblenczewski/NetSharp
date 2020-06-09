using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Stream
{
    // TODO finish off implementation
    public sealed class RawStreamNetworkWriter : RawNetworkWriterBase
    {
        /// <inheritdoc />
        public RawStreamNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxMessageSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxMessageSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (maxMessageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
                    $"The message size must be greater than 0");
            }
        }

        private void CompleteConnect(SocketAsyncEventArgs args)
        {
            AsyncOperationToken token = (AsyncOperationToken) args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    token.CompletionSource.SetResult(true);
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int) args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            ArgsPool.Return(args);
        }

        private void CompleteDisconnect(SocketAsyncEventArgs args)
        {
            AsyncOperationToken token = (AsyncOperationToken) args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    token.CompletionSource.SetResult(true);
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int) args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            ArgsPool.Return(args);
        }

        private void CompleteReceive(SocketAsyncEventArgs args)
        {
            void CompleteReceiveHeader(SocketAsyncEventArgs args)
            {
            }

            void CompleteReceiveData(SocketAsyncEventArgs args)
            {
            }
        }

        private void CompleteSend(SocketAsyncEventArgs args)
        {
            void CompleteSendHeader(SocketAsyncEventArgs args)
            {
            }

            void CompleteSendData(SocketAsyncEventArgs args)
            {
            }
        }

        private void ConfigureReceiveData(SocketAsyncEventArgs args, in RawStreamPacketHeader receivedPacketHeader)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestHeaderBuffer (as it was already parsed)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(receivedPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, receivedPacketHeader.DataSize);

            // TODO add transmission state token
            args.UserToken = new RequestReadToken(receivedPacketHeader.DataSize, receivedPacketHeader);
        }

        private void ConfigureReceiveHeader(SocketAsyncEventArgs args)
        {
            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, RawStreamPacketHeader.TotalSize);

            // TODO add transmission state token
            args.UserToken = new RequestReadToken(RawStreamPacketHeader.TotalSize, null);
        }

        private void ConfigureSendData(SocketAsyncEventArgs args, in RawStreamPacketHeader pendingPacketHeader, in ReadOnlyMemory<byte> pendingPacketData)
        {
            BufferPool.Return(args.Buffer, true);  // return and clear the requestHeaderBuffer (as it was already sent)

            byte[] pendingPacketDataBuffer = BufferPool.Rent(pendingPacketHeader.DataSize);

            args.SetBuffer(pendingPacketDataBuffer, 0, pendingPacketHeader.DataSize);

            // TODO add transmission state token
            args.UserToken = new ResponseWriteToken(pendingPacketHeader.DataSize);
        }

        private void ConfigureSendHeader(SocketAsyncEventArgs args, in RawStreamPacketHeader pendingPacketHeader)
        {
            byte[] pendingPacketHeaderBuffer = BufferPool.Rent(RawStreamPacketHeader.TotalSize);

            pendingPacketHeader.Serialise(pendingPacketHeaderBuffer);

            args.SetBuffer(pendingPacketHeaderBuffer, 0, RawStreamPacketHeader.TotalSize);

            // TODO add transmission state token
            args.UserToken = new ResponseWriteToken(RawStreamPacketHeader.TotalSize);
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
                case SocketAsyncOperation.Connect:
                    CompleteConnect(args);
                    break;

                case SocketAsyncOperation.Disconnect:
                    CompleteDisconnect(args);
                    break;

                case SocketAsyncOperation.Receive:
                    CompleteReceive(args);
                    break;

                case SocketAsyncOperation.Send:
                    CompleteSend(args);
                    break;
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
        }

        /// <inheritdoc />
        public override void Connect(EndPoint remoteEndPoint)
        {
            Connection.Connect(remoteEndPoint);
        }

        /// <inheritdoc />
        public override ValueTask ConnectAsync(EndPoint remoteEndPoint)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            args.RemoteEndPoint = remoteEndPoint;

            AsyncOperationToken token = new AsyncOperationToken(tcs);
            args.UserToken = token;

            if (Connection.ConnectAsync(args))
            {
                return new ValueTask(tcs.Task);
            }

            ArgsPool.Return(args);

            return new ValueTask();
        }

        public void Disconnect(bool reuseSocket)
        {
            Connection.Disconnect(reuseSocket);
        }

        public ValueTask DisconnectAsync(bool reuseSocket)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            args.DisconnectReuseSocket = reuseSocket;

            AsyncOperationToken token = new AsyncOperationToken(tcs);
            args.UserToken = token;

            if (Connection.DisconnectAsync(args))
            {
                return new ValueTask(tcs.Task);
            }

            ArgsPool.Return(args);

            return new ValueTask();
        }

        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }

        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }

        private readonly struct RequestReadToken
        {
            public readonly int BytesToTransfer;
            public readonly RawStreamPacketHeader? Header;

            public RequestReadToken(int bytesToTransfer, in RawStreamPacketHeader? header)
            {
                BytesToTransfer = bytesToTransfer;

                Header = header;
            }
        }

        private readonly struct ResponseWriteToken
        {
            public readonly int BytesToTransfer;

            public ResponseWriteToken(int bytesToTransfer)
            {
                BytesToTransfer = bytesToTransfer;
            }
        }
    }
}