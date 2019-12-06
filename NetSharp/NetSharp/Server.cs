using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Interfaces;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;
using NetSharp.Utils.Socket_Options;

namespace NetSharp
{
    /// <summary>
    /// Represents a method that receives a request packet of the given type (<typeparamref name="TReq"/>) and
    /// handles the request, returning a response packet of the given type (<typeparamref name="TRep"/>).
    /// </summary>
    /// <typeparam name="TReq">The type of request packet handled by this delegate method.</typeparam>
    /// <typeparam name="TRep">The type of response packet returned by this delegate method.</typeparam>
    /// <param name="requestPacket">The request packet that should be handled by this delegate method.</param>
    /// <param name="remoteEndPoint">The remote endpoint from which the request originated.</param>
    /// <returns>The response packet to send back to the remote endpoint from which the request originated.</returns>
    public delegate TRep ComplexPacketHandler<in TReq, out TRep>(TReq requestPacket, EndPoint remoteEndPoint)
        where TReq : class, IRequestPacket, new() where TRep : class, IResponsePacket<TReq>, new();

    /// <summary>
    /// Represents a method that receives a simple request packet of the given type (<typeparamref name="TReq"/>) and
    /// handles the request, not returning any response packets.
    /// </summary>
    /// <typeparam name="TReq">The type of request packet handled by this delegate method.</typeparam>
    /// <param name="requestPacket">The request packet that should be handled by this delegate method.</param>
    /// <param name="remoteEndPoint">The remote endpoint from which the request originated.</param>
    public delegate void SimplePacketHandler<in TReq>(TReq requestPacket, EndPoint remoteEndPoint)
        where TReq : class, IRequestPacket, new();

    /// <summary>
    /// Provides methods for handling connected <see cref="IClient"/> instances.
    /// </summary>
    public abstract class Server : Connection, IServer, IDisposable
    {
        /// <summary>
        /// Cancellation token source for the <see cref="TryBindAsync(IPAddress,int)"/> method.
        /// </summary>
        private readonly CancellationTokenSource bindSocketCancellationTokenSource;

        /// <summary>
        /// Maps a packet type id to the complex packet handler for that packet type.
        /// </summary>
        private readonly ConcurrentDictionary<uint, Func<IRequestPacket, EndPoint, IResponsePacket<IRequestPacket>>>
            complexPacketHandlers;

        /// <summary>
        /// Maps a packet type id to the raw packet deserialiser that deserialises raw packets to
        /// <see cref="IRequestPacket"/> implementors.
        /// </summary>
        private readonly ConcurrentDictionary<uint, RawRequestPacketDeserialiser> requestPacketDeserialisers;

        /// <summary>
        /// Maps a packet type id to the simple packet handler for that packet type.
        /// </summary>
        private readonly ConcurrentDictionary<uint, Action<IRequestPacket, EndPoint>> simplePacketHandlers;

        /// <summary>
        /// The maximum number of connections that are allowed in the connection backlog.
        /// </summary>
        protected const int PendingConnectionBacklog = 100;

        /// <summary>
        /// The default timeout value for all network operations.
        /// </summary>
        protected static readonly TimeSpan DefaultNetworkOperationTimeout = TimeSpan.FromMilliseconds(10_000);

        /// <summary>
        /// Cancellation token source to stop handling client sockets when the server should be shut down.
        /// </summary>
        protected readonly CancellationTokenSource serverShutdownCancellationTokenSource;

        /// <summary>
        /// The <see cref="Socket"/> underlying the connection.
        /// </summary>
        protected readonly Socket socket;

        /// <summary>
        /// Backing field for the <see cref="SocketOptions"/> property.
        /// </summary>
        protected readonly SocketOptions socketOptions;

        /// <summary>
        /// Whether the server should be ran.
        /// </summary>
        protected volatile bool runServer;

        /// <summary>
        /// Initialises a new instance of the <see cref="Server"/> class.
        /// </summary>
        private Server()
        {
            serverShutdownCancellationTokenSource = new CancellationTokenSource();
            bindSocketCancellationTokenSource = new CancellationTokenSource();

            requestPacketDeserialisers = new ConcurrentDictionary<uint, RawRequestPacketDeserialiser>();

            simplePacketHandlers = new ConcurrentDictionary<uint, Action<IRequestPacket, EndPoint>>();
            complexPacketHandlers =
                new ConcurrentDictionary<uint, Func<IRequestPacket, EndPoint, IResponsePacket<IRequestPacket>>>();

            RegisterInternalPacketHandlers();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketOptions = new DefaultSocketOptions(ref socket);
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="socketType">The socket type for the underlying socket.</param>
        /// <param name="protocolType">The protocol type for the underlying socket.</param>
        /// <param name="socketManager">The <see cref="Utils.Socket_Options.SocketOptions"/> manager to use.</param>
        protected Server(SocketType socketType, ProtocolType protocolType, SocketOptionManager socketManager) : this()
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
        /// Destroys an instance of the <see cref="Server"/> class.
        /// </summary>
        ~Server()
        {
            Dispose(false);
        }

        /// <summary>
        /// Represents a method that receives a raw packet, and deserialises it into an <see cref="IRequestPacket"/> implementor.
        /// </summary>
        /// <param name="rawPacket">The raw packet that was received from the network.</param>
        /// <returns>The deserialised instance of the packet.</returns>
        private delegate IRequestPacket RawRequestPacketDeserialiser(in Packet rawPacket);

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
        /// The configured socket options for the underlying connection.
        /// </summary>
        public SocketOptions SocketOptions
        {
            get { return socketOptions; }
        }

        /// <summary>
        /// Registers packet handlers for every internal library packet.
        /// </summary>
        private void RegisterInternalPacketHandlers()
        {
            TryRegisterSimplePacketHandler((DisconnectPacket packet, EndPoint remoteEndPoint) =>
            {
                OnClientDisconnected(remoteEndPoint);
            });

            TryRegisterSimplePacketHandler((SimpleDataPacket packet, EndPoint remoteEndPoint) =>
            {
#if DEBUG
                logger.LogMessage($"Received {packet.RequestBuffer.Length} bytes from {remoteEndPoint}");
#endif
            });

            TryRegisterComplexPacketHandler((ConnectPacket packet, EndPoint remoteEndPoint) =>
            {
                OnClientConnected(remoteEndPoint);

                return new ConnectResponsePacket { RequestPacket = packet };
            });

            TryRegisterComplexPacketHandler((PingPacket packet, EndPoint remoteEndPoint) =>
                new PingResponsePacket { RequestPacket = packet });

            TryRegisterComplexPacketHandler((DataPacket packet, EndPoint remoteEndPoint) =>
            {
#if DEBUG
                logger.LogMessage($"Received {packet.RequestBuffer.Length} bytes from {remoteEndPoint}");
                logger.LogMessage($"Sending {packet.RequestBuffer.Length} bytes to {remoteEndPoint}");
#endif
                return new DataResponsePacket { RequestPacket = packet, ResponseBuffer = packet.RequestBuffer };
            });
        }

        /// <summary>
        /// Deserialises the given <see cref="Packet"/> struct into an <see cref="IRequestPacket"/> implementor.
        /// </summary>
        /// <param name="packetType">The type id of packet that we should deserialise to.</param>
        /// <param name="rawRequestPacket">The packet that should be deserialised.</param>
        /// <returns>The deserialised packet instance, cast to the <see cref="IRequestPacket"/> interface.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected IRequestPacket? DeserialiseRequestPacket(uint packetType, in Packet rawRequestPacket)
        {
            if (requestPacketDeserialisers.TryGetValue(packetType, out RawRequestPacketDeserialiser deserialiser))
            {
                return deserialiser.Invoke(rawRequestPacket);
            }
#if DEBUG
            logger.LogWarning($"No packet deserialiser was registered for packet of type {packetType}");
#endif
            return default;
        }

        /// <summary>
        /// Disposes of this <see cref="Server"/> instance.
        /// </summary>
        /// <param name="disposing">Whether this instance is being disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                bindSocketCancellationTokenSource?.Cancel();
                bindSocketCancellationTokenSource?.Dispose();

                socket?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Provides a task that represents the handling of a client.
        /// </summary>
        /// <param name="clientHandlerArgsObj">The object representing the passed <see cref="ClientHandlerArgs"/> instance.</param>
        protected async Task DoHandleClientAsync(object clientHandlerArgsObj)
        {
            ClientHandlerArgs clientHandlerArgs = (ClientHandlerArgs)clientHandlerArgsObj;

            try
            {
                await HandleClientAsync(clientHandlerArgs);
            }
            catch (TaskCanceledException) { logger.LogMessage("Client handling was cancelled via a task cancellation."); }
            catch (OperationCanceledException) { logger.LogMessage("Client handling was cancelled via an operation cancellation."); }
            catch (Exception ex)
            {
                logger.LogException("Exception during client handling", ex);
            }
            finally
            {
                if (clientHandlerArgs.ClientSocket != null)
                {
                    logger.LogMessage("Closing and releasing all resources associated with client handler socket");

                    clientHandlerArgs.ClientSocket.Close(1);
                    clientHandlerArgs.ClientSocket.Shutdown(SocketShutdown.Both);
                    clientHandlerArgs.ClientSocket.Disconnect(true);
                    clientHandlerArgs.ClientSocket.Dispose();
                }
            }
        }

        /// <summary>
        /// Handles a new client asynchronously.
        /// </summary>
        /// <param name="args">The client handler arguments that should be passed to the client handler.</param>
        protected abstract Task HandleClientAsync(ClientHandlerArgs args);

        /// <summary>
        /// Handles the given request packet with a registered packet handler. In this case, a complex packet handler
        /// will override any registered simple packet handlers.
        /// </summary>
        /// <param name="packetType">The type id of the packet that we should handle.</param>
        /// <param name="requestPacket">The packet instance that should be handled.</param>
        /// <param name="remoteEndPoint">The remote endpoint from which the request packet originated.</param>
        /// <returns>The response packet that should be sent back to the remote endpoint.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected IResponsePacket<IRequestPacket>? HandleRequestPacket(uint packetType, in IRequestPacket requestPacket,
            in EndPoint remoteEndPoint)
        {
            try
            {
                if (complexPacketHandlers.ContainsKey(packetType))
                {
                    IResponsePacket<IRequestPacket> response =
                        (IResponsePacket<IRequestPacket>)complexPacketHandlers[packetType]
                            .DynamicInvoke(requestPacket, remoteEndPoint);

                    return response;
                }

                if (simplePacketHandlers.ContainsKey(packetType))
                {
                    simplePacketHandlers[packetType].DynamicInvoke(requestPacket, remoteEndPoint);
                    return null;
                }
#if DEBUG
                logger.LogWarning($"No packet handler was registered for packet of type {packetType}");
#endif
            }
            catch (Exception ex)
            {
                logger.LogException(
                    $"Exception when invoking packet handler for packet (type: {packetType}) received from {remoteEndPoint}",
                    ex);
            }

            return default;
        }

        /// <summary>
        /// Invokes the <see cref="ClientConnected"/> event.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint with which a connection was made.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnClientConnected(EndPoint remoteEndPoint) => ClientConnected?.Invoke(remoteEndPoint);

        /// <summary>
        /// Invokes the <see cref="ClientDisconnected"/> event.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint with which a connection was lost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnClientDisconnected(EndPoint remoteEndPoint) => ClientDisconnected?.Invoke(remoteEndPoint);

        /// <summary>
        /// Invokes the <see cref="ServerStarted"/> event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnServerStarted() => ServerStarted?.Invoke();

        /// <summary>
        /// Invokes the <see cref="ServerStopped"/> event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnServerStopped() => ServerStopped?.Invoke();

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local address and port. Blocks. Does not timeout.
        /// </summary>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected bool TryBind(IPAddress localAddress, int localPort) =>
            TryBindAsync(localAddress, localPort, Timeout.InfiniteTimeSpan).Result;

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local address and port. Blocks.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected bool TryBind(IPAddress localAddress, int localPort, TimeSpan timeout) =>
            TryBindAsync(localAddress, localPort, timeout).Result;

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local address and port. Does not block.
        /// Does not timeout.
        /// </summary>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected async Task<bool> TryBindAsync(IPAddress localAddress, int localPort) =>
            await TryBindAsync(localAddress, localPort, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local address and port. Does not block.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected async Task<bool> TryBindAsync(IPAddress localAddress, int localPort, TimeSpan timeout) =>
            await TryBindAsync(new IPEndPoint(localAddress, localPort), timeout);

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local endpoint. Does not block.
        /// Does not timeout.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected async Task<bool> TryBindAsync(EndPoint localEndPoint) =>
            await TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to asynchronously bind the underlying socket to the given local endpoint. Does not block.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        protected async Task<bool> TryBindAsync(EndPoint localEndPoint, TimeSpan timeout)
        {
            try
            {
                bindSocketCancellationTokenSource.CancelAfter(timeout);

                return await Task.Run(() =>
                {
                    socket.Bind(localEndPoint);

                    return true;
                }, bindSocketCancellationTokenSource.Token);
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
        public abstract Task RunAsync(EndPoint localEndPoint);

        /// <inheritdoc />
        public void Shutdown()
        {
            runServer = false;
            logger.LogMessage("Signalling server shutdown to all client handlers...");
            serverShutdownCancellationTokenSource.Cancel();
        }

        /// <inheritdoc />
        public bool TryDeregisterComplexPacketHandler<Req, Rep>(out ComplexPacketHandler<Req, Rep>? oldHandlerDelegate)
            where Req : class, IRequestPacket, new() where Rep : class, IResponsePacket<Req>, new()
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();
            oldHandlerDelegate = default;

            try
            {
                requestPacketDeserialisers.TryRemove(packetTypeId, out _);

                if (!complexPacketHandlers.TryGetValue(packetTypeId,
                    out Func<IRequestPacket, EndPoint, IResponsePacket<IRequestPacket>> oldDelegate))
                    return false;

                oldHandlerDelegate = (p, ep) => (Rep)oldDelegate(p, ep);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogException("Exception when deregistering complex packet handler", ex);
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryDeregisterSimplePacketHandler<Req>(out SimplePacketHandler<Req>? oldHandlerDelegate)
            where Req : class, IRequestPacket, new()
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();
            oldHandlerDelegate = default;

            try
            {
                requestPacketDeserialisers.TryRemove(packetTypeId, out _);

                if (!simplePacketHandlers.TryGetValue(packetTypeId, out Action<IRequestPacket, EndPoint> oldDelegate))
                    return false;

                oldHandlerDelegate = (p, ep) => oldDelegate(p, ep);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogException("Exception when deregistering simple packet handler", ex);
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryRegisterComplexPacketHandler<Req, Rep>(ComplexPacketHandler<Req, Rep> handlerDelegate)
            where Req : class, IRequestPacket, new() where Rep : class, IResponsePacket<Req>, new()
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();

            static IRequestPacket PacketDeserialiser(in Packet packet)
            {
                Req request = new Req();
                request.Deserialise(packet.Buffer);
                request.AfterDeserialisation();

                return request;
            }

            IResponsePacket<IRequestPacket> MappedHandlerDelegate(IRequestPacket p, EndPoint ep)
            {
                Rep responsePacket = handlerDelegate((Req)p, ep);
                return responsePacket;
            }

            try
            {
                requestPacketDeserialisers.AddOrUpdate(packetTypeId,
                    key => PacketDeserialiser,
                    (key, oldDeserialiser) => PacketDeserialiser);

                complexPacketHandlers.AddOrUpdate(packetTypeId,
                    key => MappedHandlerDelegate,
                    (key, oldDelegate) => MappedHandlerDelegate);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogException("Exception when registering complex packet handler", ex);
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryRegisterSimplePacketHandler<Req>(SimplePacketHandler<Req> handlerDelegate)
            where Req : class, IRequestPacket, new()
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();

            static IRequestPacket PacketDeserialiser(in Packet packet)
            {
                Req request = new Req();
                request.Deserialise(packet.Buffer);
                request.AfterDeserialisation();

                return request;
            }

            void MappedHandlerDelegate(IRequestPacket p, EndPoint ep) => handlerDelegate((Req)p, ep);

            try
            {
                requestPacketDeserialisers.AddOrUpdate(packetTypeId,
                    key => PacketDeserialiser,
                    (key, oldDeserialiser) => PacketDeserialiser);

                simplePacketHandlers.AddOrUpdate(packetTypeId,
                    key => MappedHandlerDelegate,
                    (key, oldDelegate) => MappedHandlerDelegate);

                return true;
            }
            catch (Exception ex)
            {
                logger.LogException("Exception when registering simple packet handler", ex);
            }

            return false;
        }

        /// <summary>
        /// Holds information about the arguments passed to every client handler task.
        /// </summary>
        protected readonly struct ClientHandlerArgs
        {
            /// <summary>
            /// The remote endpoint for the client being handled.
            /// </summary>
            public readonly EndPoint ClientEndPoint;

            /// <summary>
            /// The client handler socket for the client being handled. Is only set if using TCP.
            /// </summary>
            public readonly Socket? ClientSocket;

            /// <summary>
            /// Initialises a new instance of the <see cref="ClientHandlerArgs"/> struct.
            /// </summary>
            /// <param name="remoteEndPoint">The remote endpoint of the client that should be handled.</param>
            /// <param name="handlerSocket">The handler socket of the client that should be handled.</param>
            private ClientHandlerArgs(EndPoint remoteEndPoint, Socket? handlerSocket)
            {
                ClientEndPoint = remoteEndPoint;

                ClientSocket = handlerSocket;
            }

            /// <summary>
            /// Constructs a new instance of the <see cref="ClientHandlerArgs"/> for a TCP client.
            /// </summary>
            /// <returns>A new instance of the <see cref="ClientHandlerArgs"/>, setup for a TCP client.</returns>
            public static ClientHandlerArgs ForTcpClientHandler(in Socket clientHandlerSocket)
            {
                return new ClientHandlerArgs(clientHandlerSocket.RemoteEndPoint, clientHandlerSocket);
            }

            /// <summary>
            /// Constructs a new instance of the <see cref="ClientHandlerArgs"/> for a UDP client.
            /// </summary>
            /// <returns>A new instance of the <see cref="ClientHandlerArgs"/>, setup for a UDP client.</returns>
            public static ClientHandlerArgs ForUdpClientHandler(in EndPoint clientEndPoint)
            {
                return new ClientHandlerArgs(clientEndPoint, null);
            }
        }
    }
}