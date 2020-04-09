using NetSharp.Packets;

using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace NetSharp.Sockets
{
    public abstract class SocketServer : SocketConnection
    {
        protected readonly ConcurrentDictionary<EndPoint, Task> ConnectedClientHandlerTasks;

        protected readonly ArrayPool<byte> BufferPool;

        protected readonly ObjectPool<SocketAsyncEventArgs> SocketArgsPool;

        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
            BufferPool = ArrayPool<byte>.Create(NetworkPacket.TotalSize, 10);

            SocketArgsPool = new DefaultObjectPool<SocketAsyncEventArgs>(new PooledSocketAsyncEventArgsPolicy());

            ConnectedClientHandlerTasks = new ConcurrentDictionary<EndPoint, Task>();
        }

        protected abstract SocketAsyncEventArgs GenerateConnectionArgs(EndPoint remoteEndPoint);

        protected abstract void DestroyConnectionArgs(SocketAsyncEventArgs remoteConnectionArgs);

        protected abstract Task HandleClient(SocketAsyncEventArgs clientArgs,
            CancellationToken cancellationToken = default);

        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }

    public class PooledSocketAsyncEventArgsPolicy : IPooledObjectPolicy<SocketAsyncEventArgs>
    {
        public SocketAsyncEventArgs Create()
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();

            args.Completed += SocketAsyncOperations.HandleIoCompleted;

            return args;
        }

        public bool Return(SocketAsyncEventArgs obj)
        {
            return true;
        }
    }
}