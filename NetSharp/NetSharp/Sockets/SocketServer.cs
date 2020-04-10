using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    //TODO document class
    public abstract class SocketServer : SocketConnection
    {
        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
        }

        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }
}