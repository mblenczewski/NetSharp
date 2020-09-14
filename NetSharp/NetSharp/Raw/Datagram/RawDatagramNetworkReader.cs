using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw.Datagram
{
    /// <summary>
    /// Represents a method that handles a request received by a <see cref="RawDatagramNetworkReader" />.
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
    public delegate bool RawDatagramRequestHandler(
        EndPoint remoteEndPoint,
        in ReadOnlyMemory<byte> requestBuffer,
        int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    /// <summary>
    /// Implements a raw network reader using a datagram-based protocol.
    /// </summary>
    public sealed class RawDatagramNetworkReader : RawNetworkReaderBase
    {
        private readonly int datagramSize;

        private readonly RawDatagramRequestHandler requestHandler;

        /// <inheritdoc cref="RawNetworkReaderBase(ref Socket, EndPoint, int, int, uint)" />
        public RawDatagramNetworkReader(
            ref Socket rawConnection,
            RawDatagramRequestHandler? requestHandler,
            EndPoint defaultEndPoint,
            int datagramSize,
            int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0)
            : base(ref rawConnection, defaultEndPoint, datagramSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (datagramSize <= 0 || datagramSize > MaxDatagramSize)
            {
                throw new ArgumentOutOfRangeException(nameof(datagramSize), datagramSize, Properties.Resources.RawDatagramSizeError);
            }

            this.datagramSize = datagramSize;

            this.requestHandler = requestHandler ?? DefaultRequestHandler;
        }

        /// <inheritdoc />
        public override void Start(ushort concurrentReadTasks)
        {
            for (ushort i = 0; i < concurrentReadTasks; i++)
            {
                StartDefaultReceiveFrom();
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

        private static bool DefaultRequestHandler(
            EndPoint remoteEndPoint,
            in ReadOnlyMemory<byte> requestBuffer,
            int receivedRequestBytes,
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

                        StartSendTo(args);
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
        private void ConfigureAsyncReceiveFrom(SocketAsyncEventArgs args)
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

        private void StartDefaultReceiveFrom()
        {
            if (!ConnectionDisposed)
            {
                SocketAsyncEventArgs args = ArgsPool.Rent();

                ConfigureAsyncReceiveFrom(args);

                StartReceiveFrom(args);
            }
        }

        private void StartReceiveFrom(SocketAsyncEventArgs args)
        {
            if (!ConnectionDisposed && Connection.ReceiveFromAsync(args))
            {
                return;
            }

            StartDefaultReceiveFrom();
            CompleteReceiveFrom(args);
        }

        private void StartSendTo(SocketAsyncEventArgs args)
        {
            if (!ConnectionDisposed && Connection.SendToAsync(args))
            {
                return;
            }

            CompleteSendTo(args);
        }
    }
}
