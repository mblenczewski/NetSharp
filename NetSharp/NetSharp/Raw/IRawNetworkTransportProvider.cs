using NetSharp.Raw.Datagram;
using NetSharp.Raw.Stream;

using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw
{
    public interface IRawNetworkTransportProvider
    {
        SocketType TransportProtocolType { get; }

        RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0);

        RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0);
    }

    public sealed class DatagramRawNetworkTransportProvider : IRawNetworkTransportProvider
    {
        private readonly ushort datagramSize;

        public DatagramRawNetworkTransportProvider(ushort datagramSize)
        {
            this.datagramSize = datagramSize;
        }

        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Dgram;

        /// <inheritdoc />
        public RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint,
            NetworkRequestHandler? requestHandler, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0)
        {
            return new RawDatagramNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawDatagramNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }

    public sealed class StreamRawNetworkTransportProvider : IRawNetworkTransportProvider
    {
        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Stream;

        /// <inheritdoc />
        public RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler,
            int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawStreamNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawStreamNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }
}