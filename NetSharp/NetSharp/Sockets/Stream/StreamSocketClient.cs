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

        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            throw new System.NotImplementedException();
        }

        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {
            throw new System.NotImplementedException();
        }

        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
        {
            throw new System.NotImplementedException();
        }

        protected override void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            throw new System.NotImplementedException();
        }

        protected override void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            throw new System.NotImplementedException();
        }
    }
}