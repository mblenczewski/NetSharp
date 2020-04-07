using NetSharp.Packets;

using System.Buffers;
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

        protected readonly ArrayPool<byte> BufferPool;

        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
            BufferPool = ArrayPool<byte>.Create(NetworkPacket.TotalSize, 10);

            ConnectedClientHandlerTasks = new ConcurrentDictionary<EndPoint, Task>();
        }

        protected abstract SocketAsyncEventArgs GenerateConnectionArgs(EndPoint remoteEndPoint);

        protected abstract void DestroyConnectionArgs(SocketAsyncEventArgs remoteConnectionArgs);

        protected abstract Task HandleClient(SocketAsyncEventArgs clientArgs,
            CancellationToken cancellationToken = default);

        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }
}