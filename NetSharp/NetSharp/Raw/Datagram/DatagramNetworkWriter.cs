using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Datagram
{
    public sealed class DatagramNetworkWriter : NetworkWriterBase<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public DatagramNetworkWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0) : base(ref rawConnection, defaultEndPoint, maxPooledBufferSize, maxPooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs args)
        {
            AsyncDatagramReadToken token = (AsyncDatagramReadToken)args.UserToken;

            byte[] receiveBuffer = token.TransmissionBuffer;

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
            StateObjectPool.Return(args);
        }

        private void CompleteSendTo(SocketAsyncEventArgs args)
        {
            AsyncDatagramWriteToken token = (AsyncDatagramWriteToken)args.UserToken;

            byte[] sendBuffer = token.TransmissionBuffer;

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
            StateObjectPool.Return(args);
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Connect:
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
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > BufferSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {BufferSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            byte[] transmissionBuffer = BufferPool.Rent(BufferSize);

            int readBytes = Connection.ReceiveFrom(transmissionBuffer, flags, ref remoteEndPoint);

            transmissionBuffer.CopyTo(readBuffer);
            BufferPool.Return(transmissionBuffer, true);

            return readBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = readBuffer.Length;
            if (totalBytes > BufferSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {BufferSize} bytes",
                    nameof(readBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = StateObjectPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(BufferSize);

            args.SetBuffer(transmissionBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncDatagramReadToken token = new AsyncDatagramReadToken(tcs, ref transmissionBuffer, in readBuffer);
            args.UserToken = token;

            if (Connection.ReceiveFromAsync(args)) return new ValueTask<int>(tcs.Task);

            int result = args.BytesTransferred;

            transmissionBuffer.CopyTo(readBuffer);

            BufferPool.Return(transmissionBuffer, true);
            StateObjectPool.Return(args);

            return new ValueTask<int>(result);
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > BufferSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {BufferSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            byte[] transmissionBuffer = BufferPool.Rent(BufferSize);
            writeBuffer.CopyTo(transmissionBuffer);

            int writtenBytes = Connection.SendTo(transmissionBuffer, flags, remoteEndPoint);

            BufferPool.Return(transmissionBuffer);

            return writtenBytes;
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            int totalBytes = writeBuffer.Length;
            if (totalBytes > BufferSize)
            {
                throw new ArgumentException(
                    $"Cannot rent a temporary buffer of size: {totalBytes} bytes; maximum temporary buffer size: {BufferSize} bytes",
                    nameof(writeBuffer.Length)
                );
            }

            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs args = StateObjectPool.Rent();

            byte[] transmissionBuffer = BufferPool.Rent(BufferSize);
            writeBuffer.CopyTo(transmissionBuffer);

            args.SetBuffer(transmissionBuffer);

            args.RemoteEndPoint = remoteEndPoint;
            args.SocketFlags = flags;

            AsyncDatagramWriteToken token = new AsyncDatagramWriteToken(tcs, ref transmissionBuffer);
            args.UserToken = token;

            if (Connection.SendToAsync(args)) return new ValueTask<int>(tcs.Task);

            int result = args.BytesTransferred;

            BufferPool.Return(transmissionBuffer, true);
            StateObjectPool.Return(args);

            return new ValueTask<int>(result);
        }

        private readonly struct AsyncDatagramReadToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly byte[] TransmissionBuffer;
            public readonly Memory<byte> UserBuffer;

            public AsyncDatagramReadToken(TaskCompletionSource<int> completionSource, ref byte[] transmissionBuffer, in Memory<byte> userBuffer)
            {
                CompletionSource = completionSource;

                TransmissionBuffer = transmissionBuffer;

                UserBuffer = userBuffer;
            }
        }

        private readonly struct AsyncDatagramWriteToken
        {
            public readonly TaskCompletionSource<int> CompletionSource;
            public readonly byte[] TransmissionBuffer;

            public AsyncDatagramWriteToken(TaskCompletionSource<int> completionSource, ref byte[] transmissionBuffer)
            {
                CompletionSource = completionSource;

                TransmissionBuffer = transmissionBuffer;
            }
        }
    }
}