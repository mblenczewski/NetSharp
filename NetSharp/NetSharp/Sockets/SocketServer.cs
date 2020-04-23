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
    /// <param name="requestPacket">
    /// The request packet received by the server.
    /// </param>
    /// <param name="clientEndPoint">
    /// The client from which the packet was received.
    /// </param>
    /// <returns>
    /// The response packet which should be sent out to the client. If no packet should be sent out, this method must return <see cref="NetworkPacket.NullPacket" />.
    /// </returns>
    public delegate NetworkPacket SocketServerPacketHandler(in NetworkPacket requestPacket, in EndPoint clientEndPoint);

    /// <summary>
    /// Abstract base class for servers.
    /// </summary>
    public abstract class SocketServer : SocketConnection
    {
        /// <summary>
        /// The packet handler delegate to use to respond to incoming requests.
        /// </summary>
        protected readonly SocketServerPacketHandler PacketHandler;

        /// <summary>
        /// Constructs a new instance of the <see cref="SocketServer" /> class.
        /// </summary>
        /// <param name="connectionAddressFamily">
        /// The address family that the underlying connection should use.
        /// </param>
        /// <param name="connectionSocketType">
        /// The socket type that the underlying connection should use.
        /// </param>
        /// <param name="connectionProtocolType">
        /// The protocol type that the underlying connection should use.
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
        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType,
            in SocketServerPacketHandler packetHandler, in int pooledBufferMaxSize, in ushort preallocatedTransmissionArgs)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType, pooledBufferMaxSize, preallocatedTransmissionArgs)
        {
            PacketHandler = packetHandler;
        }

        /// <summary>
        /// The default request packet handler for servers. Simply echoes back any received packets.
        /// </summary>
        /// <param name="request">
        /// The request packet that was received.
        /// </param>
        /// <param name="remoteEndPoint">
        /// The client from which the packet was received.
        /// </param>
        /// <returns>
        /// The received packet.
        /// </returns>
        public static NetworkPacket DefaultPacketHandler(in NetworkPacket request, in EndPoint remoteEndPoint)
        {
            return request;
        }

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