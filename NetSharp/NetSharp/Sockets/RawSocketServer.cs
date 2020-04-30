using System;
using NetSharp.Packets;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Represents a method for serving request packets. This method should not throw any errors.
    /// </summary>
    /// <param name="clientEndPoint">
    /// The client from which the packet was received.
    /// </param>
    /// <param name="requestBuffer">
    /// The buffer holding the data received from the client.
    /// </param>
    /// <param name="responseBuffer">
    /// The buffer into which the optional response should be written.
    /// </param>
    /// <returns>
    /// Whether a response was generated which should be sent back to the client.
    /// </returns>
    public delegate bool RawRequestPacketHandler(in EndPoint clientEndPoint, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer);

    /// <summary>
    /// Abstract base class for servers.
    /// </summary>
    public abstract class RawSocketServer : SocketConnectionBase
    {
        /// <summary>
        /// The packet handler delegate to use to respond to incoming requests.
        /// </summary>
        protected readonly RawRequestPacketHandler PacketHandler;

        /// <summary>
        /// Constructs a new instance of the <see cref="RawSocketServer" /> class.
        /// </summary>
        /// <param name="rawConnection">
        /// The underlying <see cref="Socket"/> object which should be wrapped by this instance.
        /// </param>
        /// <param name="packetHandler">
        /// The packet handler delegate to use to respond to incoming requests.
        /// </param>
        /// <param name="pooledBufferMaxSize">
        /// The maximum size in bytes of buffers held in the buffer pool.
        /// </param>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of transmission args to preallocate.
        /// </param>
        protected RawSocketServer(ref Socket rawConnection, int pooledBufferMaxSize, ushort preallocatedTransmissionArgs, RawRequestPacketHandler? packetHandler)
            : base(ref rawConnection, pooledBufferMaxSize, preallocatedTransmissionArgs)
        {
            PacketHandler = packetHandler ?? DefaultRawPacketHandler;
        }

        /// <summary>
        /// The default request handler for servers. Simply echoes back any received data.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The client from which the packet was received.
        /// </param>
        /// <param name="requestBuffer">The data that was received.</param>
        /// <param name="responseBuffer">The data that should be sent back.</param>
        /// <returns>
        /// Whether to send back a response.
        /// </returns>
        public static bool DefaultRawPacketHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer,
            Memory<byte> responseBuffer) => requestBuffer.TryCopyTo(responseBuffer);

        /// <summary>
        /// Runs the server, handling requests from clients, until the <paramref name="cancellationToken" /> has its cancellation requested.
        /// </summary>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken" /> upon whose cancellation the server should shut down.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> representing the server's execution.
        /// </returns>
        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }
}