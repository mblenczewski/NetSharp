using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Interfaces;

namespace NetSharp.Extensions
{
    /// <summary>
    /// Provides additional methods and functionality to the <see cref="Client"/> class.
    /// </summary>
    public static class ClientExtensions
    {
        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint. Blocks until the bytes are all sent, and does
        /// not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        public static void SendBytes(this Client instance, byte[] buffer) =>
            instance.SendBytesAsync(buffer, Timeout.InfiniteTimeSpan).Wait();

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint. Blocks until the bytes are all sent, whilst
        /// observing a timeout of the given length.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <param name="timeout">The timeout after which to cancel the transmission attempt.</param>
        public static void SendBytes(this Client instance, byte[] buffer, TimeSpan timeout) =>
            instance.SendBytesAsync(buffer, timeout).Wait();

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint asynchronously. Does not block, and does not
        /// timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        public static async Task SendBytesAsync(this Client instance, byte[] buffer) =>
            await instance.SendBytesAsync(buffer, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint and waits for the response. Blocks until the
        /// bytes are all sent and the response has been received, and does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <returns>The byte buffer that was received as a response.</returns>
        public static byte[] SendBytesWithResponse(this Client instance, byte[] buffer) =>
            instance.SendBytesWithResponseAsync(buffer, Timeout.InfiniteTimeSpan).Result;

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint and waits for the response. Blocks until the
        /// bytes are all sent and the response has been received, whilst observing a timeout of the given length.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <param name="timeout">The timeout after which to cancel the transmission attempt.</param>
        /// <returns>The byte buffer that was received as a response.</returns>
        public static byte[] SendBytesWithResponse(this Client instance, byte[] buffer, TimeSpan timeout) =>
            instance.SendBytesWithResponseAsync(buffer, timeout).Result;

        /// <summary>
        /// Sends the given byte buffer to the connected remote endpoint and waits for the response asynchronously.
        /// Does not block, and does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="buffer">The bytes that should be sent to the connected remote endpoint.</param>
        /// <returns>The byte buffer received as a response to the sent buffer.</returns>
        public static async Task<byte[]> SendBytesWithResponseAsync(this Client instance, byte[] buffer) =>
            await instance.SendBytesWithResponseAsync(buffer, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Sends the given request and listens for a response of the given type. Blocks until the response is received.
        /// Does not timeout.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <typeparam name="Rep">The type of response packet to receive.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        /// <returns>The received instance.</returns>
        public static Rep SendComplex<Req, Rep>(this Client instance, Req request)
            where Req : IRequestPacket, new() where Rep : IResponsePacket<Req>, new() =>
            instance.SendComplexAsync<Req, Rep>(request, Timeout.InfiniteTimeSpan).Result;

        /// <summary>
        /// Sends the given request and listens for a response of the given type. Blocks until the response is received.
        /// Cancels the operation if the given timeout is exceeded.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <typeparam name="Rep">The type of response packet to receive.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        /// <param name="timeout">The timeout for which to wait for the operation to complete.</param>
        /// <returns>The received instance.</returns>
        public static Rep SendComplex<Req, Rep>(this Client instance, Req request, TimeSpan timeout)
            where Req : IRequestPacket, new() where Rep : IResponsePacket<Req>, new() =>
            instance.SendComplexAsync<Req, Rep>(request, timeout).Result;

        /// <summary>
        /// Sends the given request and listens for a response of the given type asynchronously. Does not block. Does not timeout.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <typeparam name="Rep">The type of response packet to receive.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        /// <returns>The received instance.</returns>
        public static async Task<Rep> SendComplexAsync<Req, Rep>(this Client instance, Req request)
            where Req : IRequestPacket, new() where Rep : IResponsePacket<Req>, new() =>
            await instance.SendComplexAsync<Req, Rep>(request, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Sends the given request without listening for a response, blocking until it is sent. Does not timeout.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        public static void SendSimple<Req>(this Client instance, Req request) where Req : IRequestPacket, new() =>
            instance.SendSimpleAsync(request, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Sends the given request without listening for a response, blocking until it is sent.
        /// Cancels the operation if the given timeout is exceeded.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        /// <param name="timeout">The timeout for which to wait for the operation to complete.</param>
        public static void SendSimple<Req>(this Client instance, Req request, TimeSpan timeout) where Req : IRequestPacket, new() =>
            instance.SendSimpleAsync(request, timeout);

        /// <summary>
        /// Sends the given request asynchronously without listening for a response, not blocking until it is sent.
        /// Does not timeout.
        /// </summary>
        /// <typeparam name="Req">The type of request packet to send.</typeparam>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="request">The request packet to send.</param>
        public static async Task SendSimpleAsync<Req>(this Client instance, Req request) where Req : IRequestPacket, new() =>
            await instance.SendSimpleAsync(request, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local address and port. Blocks. Does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to. Null if any IP address will suffice.</param>
        /// <param name="localPort">The local port to bind to. Null if any port will suffice.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public static bool TryBind(this Client instance, IPAddress? localAddress, int? localPort)
            => instance.TryBindAsync(localAddress, localPort, Timeout.InfiniteTimeSpan).Result;

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local address and port. Blocks.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to. Null if any IP address will suffice.</param>
        /// <param name="localPort">The local port to bind to. Null if any port will suffice.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public static bool TryBind(this Client instance, IPAddress? localAddress, int? localPort, TimeSpan timeout)
            => instance.TryBindAsync(localAddress, localPort, timeout).Result;

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local address and port. Does not block.
        /// Does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to. Null if any IP address will suffice.</param>
        /// <param name="localPort">The local port to bind to. Null if any port will suffice.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public static async Task<bool> TryBindAsync(this Client instance, IPAddress? localAddress, int? localPort)
            => await instance.TryBindAsync(localAddress, localPort, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to connect to the remote <see cref="Server"/> at the given <see cref="IPAddress"/> and over the
        /// given port. Does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="remoteAddress">The remote IP address to connect to.</param>
        /// <param name="remotePort">The remote port to connect over.</param>
        /// <returns>Whether the connection was successful or not.</returns>
        public static bool TryConnect(this Client instance, IPAddress remoteAddress, int remotePort) =>
            instance.TryConnectAsync(remoteAddress, remotePort).Wait(Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to connect to the remote <see cref="Server"/> at the given <see cref="IPAddress"/> and over the
        /// given port. If the timeout is exceeded the connection attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="remoteAddress">The remote IP address to connect to.</param>
        /// <param name="remotePort">The remote port to connect over.</param>
        /// <param name="timeout">The timeout within which to attempt the connection.</param>
        /// <returns>Whether the connection was successful or not.</returns>
        public static bool TryConnect(this Client instance, IPAddress remoteAddress, int remotePort, TimeSpan timeout) =>
            instance.TryConnectAsync(remoteAddress, remotePort).Wait(timeout);

        /// <summary>
        /// Attempts to connect asynchronously to the remote <see cref="Server"/> at the given <see cref="IPAddress"/>
        /// and over the given port. Does not timeout.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="remoteAddress">The remote IP address to connect to.</param>
        /// <param name="remotePort">The remote port to connect over.</param>
        /// <returns>Whether the connection was successful or not.</returns>
        public static async Task<bool> TryConnectAsync(this Client instance, IPAddress remoteAddress, int remotePort) =>
            await instance.TryConnectAsync(remoteAddress, remotePort, Timeout.InfiniteTimeSpan);
    }
}