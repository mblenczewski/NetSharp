using System.Net.Sockets;

namespace NetSharp.Sockets.Datagram
{
    public class DatagramSocketClient : SocketClient
    {
        public DatagramSocketClient(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Dgram, in connectionProtocolType)
        {
        }
    }
}