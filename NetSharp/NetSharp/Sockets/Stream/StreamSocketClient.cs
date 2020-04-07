using System.Net;
using System.Net.Sockets;

namespace NetSharp.Sockets.Stream
{
    public class StreamSocketClient : SocketClient
    {
        public StreamSocketClient(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Stream, in connectionProtocolType)
        {
        }

        public void Connect(in EndPoint remoteEndPoint)
        {
            connection.Connect(remoteEndPoint);
        }

        public void Disconnect()
        {
            connection.Disconnect(true);
        }
    }
}