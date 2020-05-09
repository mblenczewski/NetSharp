using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Datagram
{
    public sealed class RawDatagramNetworkWriter : RawNetworkWriterBase
    {
        private const int MaxDatagramSize = ushort.MaxValue - 28;

        private readonly int datagramSize;

        /// <inheritdoc />
        public RawDatagramNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int datagramSize, int pooledBuffersPerBucket = 50,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, datagramSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
            if (datagramSize <= 0 || MaxDatagramSize < datagramSize)
            {
                throw new ArgumentOutOfRangeException(nameof(datagramSize), datagramSize,
                    $"The datagram size must be greater than 0 and less than {MaxDatagramSize}");
            }

            this.datagramSize = datagramSize;
        }

        private void CompleteConnect(SocketAsyncEventArgs args)
        {
            AsyncOperationToken token = (AsyncOperationToken)args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    token.CompletionSource.SetResult(true);
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int)args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            ArgsPool.Return(args);
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            AsyncDatagramReadToken token = (AsyncDatagramReadToken)args.UserToken;

            byte[] receiveBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    receiveBuffer.CopyTo(token.UserBuffer);
                    token.CompletionSource.SetResult(args.BytesTransferred);
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int)args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(receiveBuffer, true);
            ArgsPool.Return(args);
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            AsyncDatagramWriteToken token = (AsyncDatagramWriteToken)args.UserToken;

            byte[] sendBuffer = args.Buffer;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    token.CompletionSource.SetResult(args.BytesTransferred);
                    break;

                case SocketError.OperationAborted:
                    token.CompletionSource.SetCanceled();
                    break;

                default:
                    int errorCode = (int)args.SocketError;
                    token.CompletionSource.SetException(new SocketException(errorCode));
                    break;
            }

            BufferPool.Return(sendBuffer, true);
            ArgsPool.Return(args);
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    CompleteConnect(args);
                    break;

                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);
                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    CompleteReceiveFrom(args);
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
            SocketAsyncEventArgs instance = new SocketAsyncEventArgs();
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

            if (Connection.ConnectAsync(args)) return new ValueTask(tcs.Task);

            ArgsPool.Return(args);

            return new ValueTask();
        }

        /// <inheritdoc />
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > datagramSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {datagramSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            byte[] transmissionBuffer = BufferPool.Rent(datagramSize);

            int readBytes = Connection.ReceiveFrom(transmissionBuffer, flags, ref remoteEndPoint);

            transmissionBuffer.CopyTo(readBuffer);
            BufferPool.Return(transmissionBuffer, true);

            return readBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > datagramSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {datagramSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(datagramSize);

            args.SetBuffer(transmissionBuffer, 0, datagramSize);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncDatagramReadToken token = new AsyncDatagramReadToken(tcs, in readBuffer);
            args.UserToken = token;

            if (Connection.ReceiveFromAsync(args)) return new ValueTask<int>(tcs.Task);

            // inlining CompleteReceiveFrom(SocketAsyncEventArgs) for performance
            int result = args.BytesTransferred;

            transmissionBuffer.CopyTo(readBuffer);

            BufferPool.Return(transmissionBuffer, true);
            ArgsPool.Return(args);

            return new ValueTask<int>(result);
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > datagramSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {datagramSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            byte[] transmissionBuffer = BufferPool.Rent(datagramSize);
            writeBuffer.CopyTo(transmissionBuffer);

            int writtenBytes = Connection.SendTo(transmissionBuffer, flags, remoteEndPoint);

            BufferPool.Return(transmissionBuffer);

            return writtenBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > datagramSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {datagramSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = ArgsPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(datagramSize);
            writeBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer, 0, datagramSize);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncDatagramWriteToken token = new AsyncDatagramWriteToken(tcs);
            args.UserToken = token;

            if (Connection.SendToAsync(args)) return new ValueTask<int>(tcs.Task);

            // inlining CompleteSendTo(SocketAsyncEventArgs) for performance
            int result = args.BytesTransferred;

            BufferPool.Return(transmissionBuffer, true);
            ArgsPool.Return(args);

            return new ValueTask<int>(result);
        }

        private readonly struct AsyncDatagramReadToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly Memory<byte> UserBuffer;

            public AsyncDatagramReadToken(TaskCompletionSource<int> completionSource, in Memory<byte> userBuffer)
            {
                CompletionSource = completionSource;

                UserBuffer = userBuffer;
            }
        }

        private readonly struct AsyncDatagramWriteToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;

            public AsyncDatagramWriteToken(TaskCompletionSource<int> completionSource)
            {
                CompletionSource = completionSource;
            }
        }
    }
}