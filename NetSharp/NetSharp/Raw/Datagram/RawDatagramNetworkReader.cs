using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw.Datagram
{
    public sealed class RawDatagramNetworkReader : RawNetworkReaderBase
    {
        /// <inheritdoc />
        public RawDatagramNetworkReader(ref Socket rawConnection, NetworkRequestHandler? requestHandler, EndPoint defaultEndPoint, int pooledPacketBufferSize,
            int pooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, requestHandler, pooledPacketBufferSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    byte[] responseBuffer = BufferPool.Rent(PacketBufferSize);

                    bool responseExists =
                        RequestHandler(args.RemoteEndPoint, receiveBuffer, args.BytesTransferred, responseBuffer);
                    BufferPool.Return(receiveBuffer, true);

                    if (responseExists)
                    {
                        args.SetBuffer(responseBuffer, 0, PacketBufferSize);

                        StartSendTo(args);

                        return;
                    }

                    BufferPool.Return(responseBuffer, true);
                    break;

                default:
                    BufferPool.Return(receiveBuffer, true);
                    ArgsPool.Return(args);
                    break;
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            byte[] sendBuffer = args.Buffer;

            BufferPool.Return(sendBuffer, true);
            ArgsPool.Return(args);
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.ReceiveFrom:
                    StartDefaultReceiveFrom();

                    CompleteReceiveFrom(args);
                    break;

                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);
                    break;
            }
        }

        private void StartDefaultReceiveFrom()
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                return;
            }

            SocketAsyncEventArgs args = ArgsPool.Rent();
            StartReceiveFrom(args);
        }

        private void StartReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(PacketBufferSize);

            if (ShutdownToken.IsCancellationRequested)
            {
                BufferPool.Return(receiveBuffer, true);
                ArgsPool.Return(args);

                return;
            }

            args.SetBuffer(receiveBuffer, 0, PacketBufferSize);

            if (Connection.ReceiveFromAsync(args)) return;

            StartDefaultReceiveFrom();
            CompleteReceiveFrom(args);
        }

        private void StartSendTo(SocketAsyncEventArgs args)
        {
            byte[] sendBuffer = args.Buffer;

            if (ShutdownToken.IsCancellationRequested)
            {
                BufferPool.Return(sendBuffer, true);
                ArgsPool.Return(args);

                return;
            }

            if (Connection.SendToAsync(args)) return;

            CompleteSendTo(args);
        }

        /// <inheritdoc />
        protected override bool CanReuseStateObject(ref SocketAsyncEventArgs instance)
        {
            return true;
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateStateObject()
        {
            SocketAsyncEventArgs instance = new SocketAsyncEventArgs { RemoteEndPoint = DefaultEndPoint };
            instance.Completed += HandleIoCompleted;

            return instance;
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
            instance.RemoteEndPoint = DefaultEndPoint;
        }

        /// <inheritdoc />
        public override void Start(ushort concurrentReadTasks)
        {
            for (ushort i = 0; i < concurrentReadTasks; i++)
            {
                StartDefaultReceiveFrom();
            }
        }
    }
}