using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw.Stream
{
    public delegate bool RawStreamRequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
        in Memory<byte> responseBuffer);

    public abstract class RawStreamNetworkReader : RawNetworkReaderBase
    {
        protected readonly RawStreamRequestHandler RequestHandler;

        /// <inheritdoc />
        protected RawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int maxMessageSize,
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

        /// <inheritdoc />
        protected sealed override bool CanReuseStateObject(ref SocketAsyncEventArgs instance)
        {
            return true;
        }

        protected void CloseClientConnection(SocketAsyncEventArgs args)
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

        protected abstract void CompleteAccept(SocketAsyncEventArgs args);

        protected abstract void CompleteReceive(SocketAsyncEventArgs args);

        protected abstract void CompleteSend(SocketAsyncEventArgs args);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ContinueReceive(SocketAsyncEventArgs args)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ContinueSend(SocketAsyncEventArgs args)
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
        protected sealed override SocketAsyncEventArgs CreateStateObject()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.Completed += HandleIoCompleted;

            return args;
        }

        /// <inheritdoc />
        protected sealed override void DestroyStateObject(SocketAsyncEventArgs instance)
        {
            instance.Completed -= HandleIoCompleted;
            instance.Dispose();
        }

        /// <inheritdoc />
        protected sealed override void ResetStateObject(ref SocketAsyncEventArgs instance)
        {
            instance.AcceptSocket = null;
        }

        protected void StartAccept(SocketAsyncEventArgs args)
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

        protected void StartDefaultAccept()
        {
            if (ShutdownToken.IsCancellationRequested)
            {
                return;
            }

            SocketAsyncEventArgs args = ArgsPool.Rent();
            StartAccept(args);
        }

        protected void StartReceive(SocketAsyncEventArgs args)
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

        protected void StartSend(SocketAsyncEventArgs args)
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
        public sealed override void Start(ushort concurrentReadTasks)
        {
            for (ushort i = 0; i < concurrentReadTasks; i++)
            {
                StartDefaultAccept();
            }
        }

        protected readonly struct TransmissionToken
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