using System;
using System.Net;
using System.Threading.Tasks;

namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes a server capable of asynchronously handling multiple <see cref="IClient"/> connections at once.
    /// </summary>
    public interface IServer
    {
        /// <summary>
        /// Signifies that a connection with a remote endpoint has been made.
        /// </summary>
        public event Action<EndPoint>? ClientConnected;

        //protected IResponsePacket<IRequestPacket> DeserialiseResponsePacket(in Packet)
        /// <summary>
        /// Signifies that a connection with a remote endpoint has been lost.
        /// </summary>
        public event Action<EndPoint>? ClientDisconnected;

        /// <summary>
        /// Signifies that the server was started and clients will start being accepted.
        /// </summary>
        public event Action? ServerStarted;

        /// <summary>
        /// Signifies that the server was stopped and clients will stop being accepted.
        /// </summary>
        public event Action? ServerStopped;

        /// <summary>
        /// Starts the server asynchronously and starts accepting client connections. Does not block.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        public Task RunAsync(EndPoint localEndPoint);

        /// <summary>
        /// Shuts down the server.
        /// </summary>
        public void Shutdown();
    }
}