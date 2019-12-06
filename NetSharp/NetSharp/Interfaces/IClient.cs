using System;
using System.Net;
using System.Threading.Tasks;

namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes a client capable of asynchronous communication with an <see cref="IServer"/> connection.
    /// </summary>
    public interface IClient
    {
        /// <summary>
        /// Signifies that a connection with the remote endpoint has been made.
        /// </summary>
        public event Action<EndPoint>? Connected;

        /// <summary>
        /// Signifies that the connection with the remote endpoint was severed.
        /// </summary>
        public event Action<EndPoint>? Disconnected;

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint asynchronously. Does not block, and observes
        /// a timeout of the given length.
        /// timeout.
        /// </summary>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <param name="timeout">The timeout after which to cancel the transmission attempt.</param>
        public Task SendBytesAsync(byte[] buffer, TimeSpan timeout);

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint and waits for the response asynchronously.
        /// Does not block, and observes a timeout of the given length.
        /// timeout.
        /// </summary>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <param name="timeout">
        /// The timeout after which to cancel the transmission attempt. This timeout is reused by both the 'send' and
        /// 'receive' parts of the transmission attempt, such that the maximum timeout is equal to 2 times the given
        /// value.
        /// </param>
        /// <returns>The byte buffer received as a response to the sent buffer.</returns>
        public Task<byte[]> SendBytesWithResponseAsync(byte[] buffer, TimeSpan timeout);

        /// <summary>
        /// Sends the given request and listens for a response of the given type asynchronously. Does not block.
        /// Cancels the operation if the given timeout is exceeded
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <typeparam name="Rep">The type of response packet to receive.</typeparam>
        /// <param name="request">The request packet to send.</param>
        /// <param name="timeout">The timeout for which to wait for the operation to complete.</param>
        /// <returns>The received instance.</returns>
        public Task<Rep> SendComplexAsync<Req, Rep>(Req request, TimeSpan timeout)
            where Req : IRequestPacket, new() where Rep : IResponsePacket<Req>, new();

        /// <summary>
        /// Sends the given request asynchronously without listening for a response, not blocking until it is sent.
        /// Cancels the operation if the given timeout is exceeded.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <param name="request">The request packet to send.</param>
        /// <param name="timeout">The timeout for which to wait for the operation to complete.</param>
        public Task SendSimpleAsync<Req>(Req request, TimeSpan timeout) where Req : IRequestPacket, new();

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local address and port. Does not block.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localAddress">The local IP address to bind to. Null if any IP address will suffice.</param>
        /// <param name="localPort">The local port to bind to. Null if any port will suffice.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public Task<bool> TryBindAsync(IPAddress? localAddress, int? localPort, TimeSpan timeout);

        /// <summary>
        /// Attempts to connect asynchronously to the remote <see cref="Server"/> at the given <see cref="IPAddress"/>
        /// and over the given port. If the timeout is exceeded the connection attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="remoteAddress">The remote IP address to connect to.</param>
        /// <param name="remotePort">The remote port to connect over.</param>
        /// <param name="timeout">The timeout within which to attempt the connection.</param>
        /// <returns>Whether the connection was successful or not.</returns>
        public Task<bool> TryConnectAsync(IPAddress remoteAddress, int remotePort, TimeSpan timeout);
    }
}