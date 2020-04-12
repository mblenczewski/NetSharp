using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    /// <summary>
    /// Abstract base class for servers.
    /// </summary>
    public abstract class SocketServer : SocketConnection
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="SocketServer"/> class.
        /// </summary>
        /// <inheritdoc />
        protected SocketServer(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType,
            in ProtocolType connectionProtocolType, in int maxPooledBufferLength) : base(in connectionAddressFamily,
            in connectionSocketType, in connectionProtocolType, in maxPooledBufferLength)
        {
        }

        /// <summary>
        /// Runs the server, handling requests from clients, until the <paramref name="cancellationToken"/> has its cancellation requested.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> upon whose cancellation the server should shut down.</param>
        /// <returns>A <see cref="Task"/> representing the server's execution.</returns>
        public abstract Task RunAsync(CancellationToken cancellationToken = default);
    }
}