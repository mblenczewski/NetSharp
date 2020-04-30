using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp
{
    public sealed class DatagramNetworkReader : NetworkReaderBase<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public DatagramNetworkReader(ref Socket rawConnection, NetworkRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxPooledBufferSize, int preallocatedStateObjects = 0)
            : base(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize, preallocatedStateObjects)
        {
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            RentedBufferHandle receiveBufferHandle = (RentedBufferHandle) args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    RentedBufferHandle responseBufferHandle = RentBuffer(BufferSize);

                    bool responseExists =
                        RequestHandler(args.RemoteEndPoint, receiveBufferHandle.RentedBuffer, responseBufferHandle.RentedBuffer);
                    ReturnBuffer(receiveBufferHandle);

                    if (responseExists)
                    {
                        args.SetBuffer(responseBufferHandle.RentedBuffer);
                        args.UserToken = responseBufferHandle;

                        StartSendTo(args);

                        return;
                    }

                    ReturnBuffer(responseBufferHandle);
                    break;

                default:
                    ReturnBuffer(receiveBufferHandle);
                    StateObjectPool.Return(args);
                    break;
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            RentedBufferHandle sendBufferHandle = (RentedBufferHandle)args.UserToken;

            ReturnBuffer(sendBufferHandle);
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
            RentedBufferHandle receiveBufferHandle = RentBuffer(BufferSize);

            args.SetBuffer(receiveBufferHandle.RentedBuffer, 0, BufferSize);
            args.UserToken = receiveBufferHandle;

            if (ShutdownToken.IsCancellationRequested)
            {
                ReturnBuffer(receiveBufferHandle);
                StateObjectPool.Return(args);

                return;
            }

            if (Connection.ReceiveFromAsync(args)) return;

            StartDefaultReceiveFrom();
            CompleteReceiveFrom(args);
        }

        private void StartSendTo(SocketAsyncEventArgs args)
        {
            RentedBufferHandle sendBufferHandle = (RentedBufferHandle) args.UserToken;

            if (ShutdownToken.IsCancellationRequested)
            {
                ReturnBuffer(sendBufferHandle);
                StateObjectPool.Return(args);

                return;
            }

            if (Connection.SendToAsync(args)) return;

            CompleteSendTo(args);
        }

        /// <inheritdoc />
        protected override bool CanReuseStateObject(in SocketAsyncEventArgs instance)
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

    public sealed class DatagramNetworkWriter : NetworkWriterBase<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public DatagramNetworkWriter(ref Socket rawConnection, int maxPooledBufferSize, int preallocatedStateObjects = 0) : base(ref rawConnection, maxPooledBufferSize, preallocatedStateObjects)
        {
        }

        /// <inheritdoc />
        protected override bool CanReuseStateObject(in SocketAsyncEventArgs instance)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateStateObject()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override void DestroyStateObject(SocketAsyncEventArgs instance)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override void ResetStateObject(ref SocketAsyncEventArgs instance)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            throw new NotImplementedException();
        }
    }
}