using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw.Datagram
{
    public delegate bool RawDatagramRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    public sealed class RawDatagramNetworkReader : RawNetworkReaderBase
    {
        private readonly int datagramSize;

        private readonly RawDatagramRequestHandler requestHandler;

        /// <inheritdoc />
        public RawDatagramNetworkReader(ref Socket rawConnection, RawDatagramRequestHandler? requestHandler, EndPoint defaultEndPoint, int datagramSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, datagramSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (datagramSize <= 0 || MaxDatagramSize < datagramSize)
            {
                throw new ArgumentOutOfRangeException(nameof(datagramSize), datagramSize, Properties.Resources.RawDatagramSizeError);
            }

            this.datagramSize = datagramSize;

            this.requestHandler = requestHandler ?? DefaultRequestHandler;
        }

        private static bool DefaultRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    byte[] responseBuffer = BufferPool.Rent(datagramSize);

                    bool responseExists = requestHandler(args.RemoteEndPoint, receiveBuffer, args.BytesTransferred, responseBuffer);
                    BufferPool.Return(receiveBuffer, true);

                    if (responseExists)
                    {
                        args.SetBuffer(responseBuffer, 0, datagramSize);

                        SendTo(args);
                        return;
                    }

                    BufferPool.Return(responseBuffer, true);
                    break;

                default:
                    CleanupTransmissionBufferAndState(args);
                    break;
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            CleanupTransmissionBufferAndState(args);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceiveFrom(SocketAsyncEventArgs args)
        {
            byte[] receiveBuffer = BufferPool.Rent(datagramSize);
            args.SetBuffer(receiveBuffer, 0, datagramSize);
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

        private void ReceiveFrom(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                ArgsPool.Return(args);
                return;
            }

            if (Connection.ReceiveFromAsync(args))
            {
                return;
            }

            StartDefaultReceiveFrom();
            CompleteReceiveFrom(args);
        }

        private void SendTo(SocketAsyncEventArgs args)
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                CleanupTransmissionBufferAndState(args);
                return;
            }

            if (Connection.SendToAsync(args))
            {
                return;
            }

            CompleteSendTo(args);
        }

        private void StartDefaultReceiveFrom()
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                return;
            }

            SocketAsyncEventArgs args = ArgsPool.Rent();

            ConfigureReceiveFrom(args);

            ReceiveFrom(args);
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