using NetSharp.Raw.Datagram;
using NetSharp.Raw.Stream;

using System.Net;
using System.Net.Sockets;

namespace NetSharp.Raw
{
    public interface IRawNetworkTransportProvider<TState> where TState : class
    {
        SocketType TransportProtocolType { get; }

        RawNetworkReaderBase<TState> GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0);

        RawNetworkWriterBase<TState> GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0);
    }

    public sealed class DatagramRawNetworkTransportProvider : IRawNetworkTransportProvider<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Dgram;

        /// <inheritdoc />
        public RawNetworkReaderBase<SocketAsyncEventArgs> GetReader(ref Socket rawConnection, EndPoint defaultEndPoint,
            NetworkRequestHandler? requestHandler, int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000,
            uint preallocatedStateObjects = 0)
        {
            return new RawDatagramNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase<SocketAsyncEventArgs> GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawDatagramNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }

    public sealed class StreamRawNetworkTransportProvider : IRawNetworkTransportProvider<SocketAsyncEventArgs>
    {
        /// <inheritdoc />
        public SocketType TransportProtocolType { get; } = SocketType.Stream;

        /// <inheritdoc />
        public RawNetworkReaderBase<SocketAsyncEventArgs> GetReader(ref Socket rawConnection, EndPoint defaultEndPoint, NetworkRequestHandler? requestHandler,
            int maxPooledBufferSize, int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawStreamNetworkReader(ref rawConnection, requestHandler, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }

        /// <inheritdoc />
        public RawNetworkWriterBase<SocketAsyncEventArgs> GetWriter(ref Socket rawConnection, EndPoint defaultEndPoint, int maxPooledBufferSize,
            int maxPooledBuffersPerBucket = 1000, uint preallocatedStateObjects = 0)
        {
            return new RawStreamNetworkWriter(ref rawConnection, defaultEndPoint, maxPooledBufferSize,
                maxPooledBuffersPerBucket, preallocatedStateObjects);
        }
    }
}