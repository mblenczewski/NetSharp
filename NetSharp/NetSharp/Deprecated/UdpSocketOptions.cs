using System.Net.Sockets;

namespace NetSharp.Utils.Socket_Options
{
    /// <summary>
    /// Allows for manipulation of UDP socket options.
    /// </summary>
    public sealed class UdpSocketOptions : SocketOptions
    {
        /// <inheritdoc />
        public UdpSocketOptions(ref Socket socket) : base(ref socket)
        {
        }

        /// <inheritdoc />
        public override int HopLimit
        {
            get { return (int)managedSocket.GetSocketOption(SocketOptionLevel.Udp, SocketOptionName.HopLimit); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.HopLimit, value); }
        }

        /// <inheritdoc />
        public override bool IsRoutingEnabled
        {
            get { return !(bool)managedSocket.GetSocketOption(SocketOptionLevel.Udp, SocketOptionName.DontRoute); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.DontRoute, !value); }
        }

        /// <inheritdoc />
        public override bool UseLoopback
        {
            get { return (bool)managedSocket.GetSocketOption(SocketOptionLevel.Udp, SocketOptionName.UseLoopback); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.UseLoopback, value); }
        }
    }
}