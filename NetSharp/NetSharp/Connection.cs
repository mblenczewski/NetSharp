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
            IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider, EndPoint defaultEndPoint, int packetBufferSize,
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

        protected readonly RawNetworkReaderBase<SocketAsyncEventArgs> Reader;

        protected readonly RawNetworkWriterBase<SocketAsyncEventArgs> Writer;

        private protected ConnectionBase(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType,
            IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider, EndPoint defaultEndPoint, int packetBufferSize,
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

    public sealed class ConnectionBuilder
    {
        public static ConfiguredConnectionBuilder WithCustomTransport(IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider,
            ProtocolType transportProtocol)
        {
            return new ConfiguredConnectionBuilder(transportProvider, transportProtocol);
        }

        public static ConfiguredConnectionBuilder WithDatagramTransport(ProtocolType transportProtocol = ProtocolType.Udp) =>
            WithCustomTransport(new DatagramRawNetworkTransportProvider(), transportProtocol);

        public static ConfiguredConnectionBuilder WithStreamTransport(ProtocolType transportProtocol = ProtocolType.Tcp) =>
            WithCustomTransport(new StreamRawNetworkTransportProvider(), transportProtocol);

        public sealed class ConfiguredConnectionBuilder
        {
            private readonly IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider;

            internal ConfiguredConnectionBuilder(IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider, ProtocolType transportProtocol)
            {
                this.transportProvider = transportProvider;

                SocketType = transportProvider.TransportProtocolType;

                ProtocolType = transportProtocol;
            }

            public AddressFamily AddressFamily { get; private set; } = AddressFamily.InterNetwork;
            public EndPoint DefaultEndPoint { get; private set; } = new IPEndPoint(IPAddress.Any, 0);
            public ProtocolType ProtocolType { get; }
            public SocketType SocketType { get; }

            public ConfiguredConnectionBuilder WithAddressFamily(AddressFamily addressFamily, EndPoint defaultEndPoint)
            {
                AddressFamily = addressFamily;

                DefaultEndPoint = defaultEndPoint;

                return this;
            }

            public CompletedConnectionBuilder WithDefaultSettings(int packetBufferSize) =>
                WithSettings(packetBufferSize, 1000, 0);

            public ConfiguredConnectionBuilder WithInterNetwork(EndPoint defaultEndPoint) =>
                                        WithAddressFamily(AddressFamily.InterNetwork, defaultEndPoint);

            public ConfiguredConnectionBuilder WithInterNetworkV6(EndPoint defaultEndPoint) =>
                WithAddressFamily(AddressFamily.InterNetworkV6, defaultEndPoint);

            public CompletedConnectionBuilder WithSettings(int packetBufferSize, int pooledBuffersPerBucket,
                uint preallocatedStateObjects)
            {
                return new CompletedConnectionBuilder(transportProvider, AddressFamily, DefaultEndPoint, SocketType,
                    ProtocolType, packetBufferSize, pooledBuffersPerBucket, preallocatedStateObjects);
            }

            public sealed class CompletedConnectionBuilder
            {
                private readonly IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider;

                internal CompletedConnectionBuilder(
                    IRawNetworkTransportProvider<SocketAsyncEventArgs> transportProvider, AddressFamily addressFamily, EndPoint defaultEndPoint,
                    SocketType transportProtocolType, ProtocolType transportProtocol, int maxBufferSize, int pooledBuffersPerBucket,
                    uint preallocatedStateObjects)
                {
                    this.transportProvider = transportProvider;

                    AddressFamily = addressFamily;

                    DefaultEndPoint = defaultEndPoint;

                    SocketType = transportProtocolType;

                    ProtocolType = transportProtocol;

                    BufferSize = maxBufferSize;

                    PooledBuffersPerBucket = pooledBuffersPerBucket;

                    PreallocatedStateObjects = preallocatedStateObjects;
                }

                public AddressFamily AddressFamily { get; }
                public int BufferSize { get; }
                public EndPoint DefaultEndPoint { get; }
                public int PooledBuffersPerBucket { get; }
                public uint PreallocatedStateObjects { get; }
                public ProtocolType ProtocolType { get; }
                public SocketType SocketType { get; }

                public Connection BuildDefault()
                {
                    return new Connection(AddressFamily, SocketType, ProtocolType, transportProvider, DefaultEndPoint,
                        BufferSize, PooledBuffersPerBucket, PreallocatedStateObjects);
                }
            }
        }
    }
}