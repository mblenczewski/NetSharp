﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Extensions;
using NetSharp.Interfaces;
using NetSharp.Packets.Builtin;
using NetSharp.Utils.Socket_Options;

namespace NetSharp
{
    /// <summary>
    /// Provides methods for connecting to and talking with a <see cref="IServer"/> instance.
    /// </summary>
    public abstract class Client : Connection, IClient, IDisposable
    {
        /// <summary>
        /// Provides <see cref="CancellationToken"/> instances for cancelling methods after a timeout period.
        /// </summary>
        protected readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// The <see cref="Socket"/> underlying the connection.
        /// </summary>
        protected readonly Socket socket;

        /// <summary>
        /// Backing field for the <see cref="SocketOptions"/> property.
        /// </summary>
        protected readonly SocketOptions socketOptions;

        /// <summary>
        /// The remote endpoint with which this client communicates.
        /// </summary>
        protected EndPoint remoteEndPoint;

        /// <summary>
        /// Initialises a new instance of the <see cref="Client"/> class.
        /// </summary>
        private Client()
        {
            cancellationTokenSource = new CancellationTokenSource();

            remoteEndPoint = new IPEndPoint(IPAddress.None, IPEndPoint.MinPort);
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socketOptions = new DefaultSocketOptions(ref socket);
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="socketType">The socket type for the underlying socket.</param>
        /// <param name="protocolType">The protocol type for the underlying socket.</param>
        /// <param name="socketManager">The <see cref="Utils.Socket_Options.SocketOptions"/> manager to use.</param>
        protected Client(SocketType socketType, ProtocolType protocolType, SocketOptionManager socketManager) : this()
        {
            socket = new Socket(AddressFamily.InterNetwork, socketType, protocolType);

            socketOptions = socketManager switch
            {
                SocketOptionManager.Tcp => new TcpSocketOptions(ref socket) as SocketOptions,
                SocketOptionManager.Udp => new UdpSocketOptions(ref socket) as SocketOptions,
                _ => new DefaultSocketOptions(ref socket),
            };
        }

        /// <summary>
        /// Destroys an instance of the <see cref="Client"/> class.
        /// </summary>
        ~Client()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public event Action<EndPoint>? Connected;

        /// <inheritdoc />
        public event Action<EndPoint>? Disconnected;

        /// <summary>
        /// The configured socket options for the underlying connection.
        /// </summary>
        public SocketOptions SocketOptions
        {
            get { return socketOptions; }
        }

        /// <summary>
        /// Disposes of this <see cref="Client"/> instance.
        /// </summary>
        /// <param name="disposing">Whether this instance is being disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancellationTokenSource?.Dispose();
                socket?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Invokes the <see cref="Connected"/> event.
        /// </summary>
        /// <param name="endPoint">The remote endpoint with which a connection was made.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnConnected(EndPoint endPoint) => Connected?.Invoke(endPoint);

        /// <summary>
        /// Invokes the <see cref="Disconnected"/> event.
        /// </summary>
        /// <param name="endPoint">The remote endpoint with which a connection was lost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnDisconnected(EndPoint endPoint) => Disconnected?.Invoke(endPoint);

        /// <summary>
        /// Disconnects the client from the remote endpoint.
        /// </summary>
        public void Disconnect()
        {
            this.SendSimple(new DisconnectPacket());

            socket.Shutdown(SocketShutdown.Both);
            socket.Disconnect(true);
            socket.Close();
        }

        /// <inheritdoc />
        public abstract Task SendBytesAsync(byte[] buffer, TimeSpan timeout);

        /// <inheritdoc />
        public abstract Task<byte[]> SendBytesWithResponseAsync(byte[] buffer, TimeSpan timeout);

        /// <inheritdoc />
        public abstract Task<Rep> SendComplexAsync<Req, Rep>(Req request, TimeSpan timeout)
            where Req : IRequestPacket, new() where Rep : IResponsePacket<Req>, new();

        /// <inheritdoc />
        public abstract Task SendSimpleAsync<Req>(Req request, TimeSpan timeout) where Req : IRequestPacket, new();

        /// <inheritdoc />
        public async Task<bool> TryBindAsync(IPAddress? localAddress, int? localPort, TimeSpan timeout)
        {
            EndPoint localEndPoint = new IPEndPoint(localAddress ?? IPAddress.Any, localPort ?? 0);

            try
            {
                cancellationTokenSource.CancelAfter(timeout);

                return await Task.Run(() =>
                {
                    socket.Bind(localEndPoint);

                    return true;
                }, cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on binding socket to {localEndPoint}:", ex);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> TryConnectAsync(IPAddress remoteAddress, int remotePort, TimeSpan timeout)
        {
            remoteEndPoint = new IPEndPoint(remoteAddress, remotePort);

            try
            {
                cancellationTokenSource.CancelAfter(timeout);

                return await Task.Run(async () =>
                {
                    await socket.ConnectAsync(remoteEndPoint);

                    ConnectResponsePacket connectionResponsePacket =
                        await SendComplexAsync<ConnectPacket, ConnectResponsePacket>(new ConnectPacket(), timeout);

                    OnConnected(SocketOptions.RemoteIPEndPoint);

                    return true;
                }, cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return false;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception on connection to {remoteEndPoint}:", ex);
                return false;
            }
        }
    }
}