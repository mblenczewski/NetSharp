using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    public abstract class SocketServer : SocketConnection
    {
        protected readonly ConcurrentDictionary<EndPoint, Task> ConnectedClientHandlerTasks;

        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
            ConnectedClientHandlerTasks = new ConcurrentDictionary<EndPoint, Task>();
        }

        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }
}