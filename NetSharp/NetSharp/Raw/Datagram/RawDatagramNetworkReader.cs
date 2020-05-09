using System;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw.Datagram
{
    public delegate bool RawDatagramRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        Memory<byte> responseBuffer);

    public sealed class RawDatagramNetworkReader : RawNetworkReaderBase
    {
        private const int MaxDatagramSize = ushort.MaxValue - 28;

        private readonly int datagramSize;

        private readonly RawDatagramRequestHandler requestHandler;

        /// <inheritdoc />
        public RawDatagramNetworkReader(ref Socket rawConnection, RawDatagramRequestHandler? requestHandler, EndPoint defaultEndPoint, int datagramSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, datagramSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (datagramSize <= 0 || MaxDatagramSize < datagramSize)
            {
                throw new ArgumentOutOfRangeException(nameof(datagramSize), datagramSize,
                    $"The datagram size must be greater than 0 and less than {MaxDatagramSize}");
            }

            this.datagramSize = datagramSize;

            this.requestHandler = requestHandler ?? DefaultRequestHandler;
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    byte[] responseBuffer = BufferPool.Rent(datagramSize);

                    bool responseExists =
                        requestHandler(args.RemoteEndPoint, receiveBuffer, args.BytesTransferred, responseBuffer);

                    Buffer.BlockCopy(responseBuffer, 0, receiveBuffer, 0, datagramSize);
                    BufferPool.Return(responseBuffer, true);

                    if (responseExists) StartSendTo(args);
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
            if (ShutdownToken.IsCancellationRequested)
            {
                ArgsPool.Return(args);
                return;
            }

            byte[] receiveBuffer = BufferPool.Rent(datagramSize);
            args.SetBuffer(receiveBuffer, 0, datagramSize);

            if (Connection.ReceiveFromAsync(args)) return;

            StartDefaultReceiveFrom();
            CompleteReceiveFrom(args);
        }

        private void StartSendTo(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                byte[] sendBuffer = args.Buffer;
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

        public static bool DefaultRequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
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