using System.Net;
using System.Net.Sockets;
using NetSharp.Raw;

namespace NetSharp
{
    public static class ConnectionBuilder
    {
        public static ConfiguredConnectionBuilder WithCustomTransport(
            IRawNetworkTransportProvider transportProvider, ProtocolType transportProtocol) =>
            new ConfiguredConnectionBuilder(transportProvider, transportProtocol);

        public static ConfiguredConnectionBuilder WithDatagramTransport(ushort datagramSize = 8192, ProtocolType transportProtocol = ProtocolType.Udp) =>
            WithCustomTransport(new DatagramRawNetworkTransportProvider(datagramSize), transportProtocol);

        public static ConfiguredConnectionBuilder WithStreamTransport(ProtocolType transportProtocol = ProtocolType.Tcp) =>
            WithCustomTransport(new StreamRawNetworkTransportProvider(), transportProtocol);

        public sealed class ConfiguredConnectionBuilder
        {
            private readonly IRawNetworkTransportProvider transportProvider;

            internal ConfiguredConnectionBuilder(IRawNetworkTransportProvider transportProvider, ProtocolType transportProtocol)
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
                private readonly IRawNetworkTransportProvider transportProvider;

                internal CompletedConnectionBuilder(
                    IRawNetworkTransportProvider transportProvider, AddressFamily addressFamily, EndPoint defaultEndPoint,
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