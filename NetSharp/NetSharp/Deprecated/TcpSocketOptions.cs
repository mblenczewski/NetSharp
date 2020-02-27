using System.Net.Sockets;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Allows for manipulation of TCP socket options.
    /// </summary>
    public sealed class TcpSocketOptions : SocketOptions
    {
        /// <inheritdoc />
        public TcpSocketOptions(ref Socket socket) : base(ref socket)
        {
        }

        /// <inheritdoc />
        public override int HopLimit
        {
            get { return (int)managedSocket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.HopLimit); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.HopLimit, value); }
        }

        /// <inheritdoc />
        public override bool IsRoutingEnabled
        {
            get { return !(bool)managedSocket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.DontRoute); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.DontRoute, !value); }
        }

        /// <inheritdoc />
        public override bool UseLoopback
        {
            get { return (bool)managedSocket.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.UseLoopback); }
            set { managedSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.UseLoopback, value); }
        }
    }
}