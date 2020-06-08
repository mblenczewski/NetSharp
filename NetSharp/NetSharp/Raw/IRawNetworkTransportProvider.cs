using System;
using System.Net;
using System.Net.Sockets;

using NetSharp.Raw.Datagram;
using NetSharp.Raw.Stream;

namespace NetSharp.Raw
{
    public interface IRawNetworkTransportProvider<in TReqHandler> where TReqHandler : Delegate
    {
        SocketType TransportProtocolType { get; }

        RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, TReqHandler requestHandler, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0);

        RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0);
    }

    public sealed class DatagramRawNetworkTransportProvider : IRawNetworkTransportProvider<RawDatagramRequestHandler>
    {
        private readonly ushort datagramSize;

        public DatagramRawNetworkTransportProvider(ushort datagramSize)
        {
            this.datagramSize = datagramSize;
        }

        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Dgram;

        /// <inheritdoc />
        public RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, RawDatagramRequestHandler? requestHandler,
            int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
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

    public sealed class FixedPacketRawStreamNetworkTransportProvider : IRawNetworkTransportProvider<RawStreamRequestHandler>
    {
        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Stream;

        /// <inheritdoc />
        public RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, RawStreamRequestHandler? requestHandler,
            int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new FixedPacketRawStreamNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new FixedPacketRawStreamNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }

    public sealed class VariablePacketRawStreamNetworkTransportProvider : IRawNetworkTransportProvider<RawStreamRequestHandler>
    {
        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Stream;

        /// <inheritdoc />
        public RawNetworkReaderBase GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, RawStreamRequestHandler? requestHandler,
            int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new VariablePacketRawStreamNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new VariablePacketRawStreamNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }
}