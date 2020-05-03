using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw.Datagram
{
    public sealed class DatagramNetworkReader : NetworkReaderBase<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public DatagramNetworkReader(ref Socket rawConnection, NetworkRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, requestHandler, maxPooledBufferSize,
            maxPooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    byte[] responseBuffer = BufferPool.Rent(BufferSize);

                    bool responseExists =
                        RequestHandler(args.RemoteEndPoint, receiveBuffer, args.BytesTransferred, responseBuffer);
                    BufferPool.Return(receiveBuffer, true);

                    if (responseExists)
                    {
                        args.SetBuffer(responseBuffer, 0, BufferSize);

                        StartSendTo(args);

                        return;
                    }

                    BufferPool.Return(responseBuffer, true);
                    break;

                default:
                    BufferPool.Return(receiveBuffer, true);
                    StateObjectPool.Return(args);
                    break;
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            byte[] sendBuffer = args.Buffer;

            BufferPool.Return(sendBuffer, true);
            StateObjectPool.Return(args);
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

            SocketAsyncEventArgs args = StateObjectPool.Rent();
            StartReceiveFrom(args);
        }

        private void StartReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(BufferSize);

            if (ShutdownToken.IsCancellationRequested)
            {
                BufferPool.Return(receiveBuffer, true);
                StateObjectPool.Return(args);

                return;
            }

            args.SetBuffer(receiveBuffer, 0, BufferSize);

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
                StateObjectPool.Return(args);

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