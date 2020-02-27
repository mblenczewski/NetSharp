using System.Net;
using System.Net.Sockets;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Allows for manipulation of socket options.
    /// </summary>
    public abstract class SocketOptions
    {
        /// <summary>
        /// The <see cref="Socket"/> instance whose settings are being managed.
        /// </summary>
        protected readonly Socket managedSocket;

        /// <summary>
        /// Initialises a new instance of the <see cref="SocketOptions"/> class.
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> instance whose options should be managed.</param>
        protected SocketOptions(ref Socket socket)
        {
            managedSocket = socket;
        }

        /// <summary>
        /// Whether this <see cref="Socket"/> can operate in dual IPv4 / IPv6 mode.
        /// </summary>
        public bool DualMode { get { return managedSocket.DualMode; } set { managedSocket.DualMode = value; } }

        /// <summary>
        /// Whether sending a packet flushes underlying <see cref="NetworkStream"/>.
        /// </summary>
        /// <remarks>
        /// This value is only used in a <see cref="System.Net.Sockets.TcpClient"/> instance, which uses a <see cref="NetworkStream"/>
        /// to send and receive data. A <see cref="System.Net.Sockets.UdpClient"/> is unaffected by this value.
        /// </remarks>
        public bool ForceFlush { get; set; } = true;

        /// <summary>
        /// Whether this <see cref="Socket"/> is allowed to fragment frames that are too large to send in one go.
        /// </summary>
        public bool Fragment { get { return !managedSocket.DontFragment; } set { managedSocket.DontFragment = !value; } }

        /// <summary>
        /// The hop limit for packets sent by this <see cref="Socket"/>. Comparable to IPv4s TTL (Time To Live).
        /// </summary>
        public abstract int HopLimit { get; set; }

        /// <summary>
        /// Whether a checksum should be created for each UDP packet sent.
        /// </summary>
        public bool IsChecksumEnabled
        {
            get { return (int)managedSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoChecksum) == 0; }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoChecksum, value ? 0 : -1); }
        }

        /// <summary>
        /// Whether the packet should be sent directly to its destination or allowed to be routed through multiple destinations
        /// first.
        /// </summary>
        public abstract bool IsRoutingEnabled { get; set; }

        /// <summary>
        /// The local <see cref="EndPoint"/> for the <see cref="managedSocket"/>.
        /// </summary>
        public EndPoint LocalEndPoint { get { return managedSocket.LocalEndPoint; } }

        /// <summary>
        /// The local <see cref="IPEndPoint"/> for this <see cref="Socket"/> instance.
        /// </summary>
        public EndPoint LocalIPEndPoint
        {
            get
            {
                return managedSocket?.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);
            }
        }

        /// <summary>
        /// The remote <see cref="EndPoint"/> for the <see cref="managedSocket"/>.
        /// </summary>
        public EndPoint RemoteEndPoint { get { return managedSocket.RemoteEndPoint; } }

        /// <summary>
        /// The remote <see cref="IPEndPoint"/> that this <see cref="Socket"/> instance communicates with.
        /// </summary>
        public EndPoint RemoteIPEndPoint
        {
            get
            {
                return managedSocket?.RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);
            }
        }

        /// <summary>
        /// The 'Time To Live' for this <see cref="Socket"/>.
        /// </summary>
        public short Ttl { get { return managedSocket.Ttl; } set { managedSocket.Ttl = value; } }

        /// <summary>
        /// Whether this <see cref="Socket"/> should use a loopback address and bypass hardware.
        /// </summary>
        public abstract bool UseLoopback { get; set; }
    }
}