using NetSharp.Raw;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp
{
    // TODO complete class
    public sealed class Connection : ConnectionBase
    {
        /// <inheritdoc />
        internal Connection(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType,
            IRawNetworkTransportProvider transportProvider, EndPoint defaultEndPoint, int packetBufferSize,
            int pooledBuffersPerBucket, uint preallocatedStateObjects) : base(addressFamily, socketType, protocolType,
            transportProvider, defaultEndPoint, packetBufferSize, pooledBuffersPerBucket, preallocatedStateObjects)
        {
        }

        /// <inheritdoc />
        /// TODO convert to use registered packet handlers
        protected override bool HandleRawPacketReceived(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        /// <inheritdoc />
        public override void Connect(EndPoint remoteEndPoint)
        {
            Writer.Connect(remoteEndPoint);
        }

        /// <inheritdoc />
        public override ValueTask ConnectAsync(EndPoint remoteEndPoint)
        {
            return Writer.ConnectAsync(remoteEndPoint);
        }

        /// <inheritdoc />
        public override int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            return Writer.Read(ref remoteEndPoint, readBuffer, flags);
        }

        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None)
        {
            return Writer.ReadAsync(remoteEndPoint, readBuffer, flags);
        }

        /// <inheritdoc />
        public override void Start(ushort concurrentReadTasks)
        {
            Reader.Start(concurrentReadTasks);
        }

        /// <inheritdoc />
        public override void Stop()
        {
            Reader.Stop();
        }

        /// <inheritdoc />
        public override int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            return Writer.Write(remoteEndPoint, writeBuffer, flags);
        }

        /// <inheritdoc />
        public override ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None)
        {
            return Writer.ReadAsync(remoteEndPoint, Memory<byte>.Empty, flags);
        }
    }

    // TODO complete class
    public abstract class ConnectionBase : IDisposable, INetworkReader, INetworkWriter
    {
        private readonly Socket connection;

        protected readonly RawNetworkReaderBase Reader;

        protected readonly RawNetworkWriterBase Writer;

        private protected ConnectionBase(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType,
            IRawNetworkTransportProvider transportProvider, EndPoint defaultEndPoint, int packetBufferSize,
            int pooledBuffersPerBucket, uint preallocatedStateObjects)
        {
            connection = new Socket(addressFamily, socketType, protocolType);

            Reader = transportProvider.GetReader(ref connection, defaultEndPoint, HandleRawPacketReceived,
                packetBufferSize, pooledBuffersPerBucket, preallocatedStateObjects);

            Writer = transportProvider.GetWriter(ref connection, defaultEndPoint, packetBufferSize,
                pooledBuffersPerBucket, preallocatedStateObjects);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            Writer.Dispose();

            Reader.Stop();
            Reader.Dispose();

            connection.Dispose();
        }

        protected abstract bool HandleRawPacketReceived(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            Memory<byte> responseBuffer);

        /// <inheritdoc />
        public abstract void Connect(EndPoint remoteEndPoint);

        /// <inheritdoc />
        public abstract ValueTask ConnectAsync(EndPoint remoteEndPoint);

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public abstract int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract void Start(ushort concurrentReceiveTasks);

        /// <inheritdoc />
        public abstract void Stop();

        /// <inheritdoc />
        public abstract int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);

        /// <inheritdoc />
        public abstract ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer,
            SocketFlags flags = SocketFlags.None);
    }
}