using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetSharp.Interfaces;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;
using NetSharp.Utils;
using NetSharp.Utils.Socket_Options;

namespace NetSharp.Servers
{
    /// <summary>
    /// Provides methods for UDP communication with connected <see cref="Clients.UdpClient"/> instances.
    /// </summary>
    public sealed class UdpServer : Server
    {
        /// <summary>
        /// The options that should be applied to every channel created to handle a client.
        /// </summary>
        private static readonly UnboundedChannelOptions clientChannelOptions = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        };

        /// <summary>
        /// Holds currently connected and active clients, as well as their current received packet queues.
        /// </summary>
        private readonly ConcurrentDictionary<EndPoint, Channel<SerialisedPacket>> activeClients;

        /// <inheritdoc />
        protected override async Task HandleClientAsync(ClientHandlerArgs args, CancellationToken cancellationToken)
        {
            EndPoint clientEndPoint = args.ClientEndPoint;
            Channel<SerialisedPacket> clientPacketBuffer = activeClients[clientEndPoint];

            logger.LogMessage($"Initialised client handler for client socket: [Remote EP: {clientEndPoint}]");

            try
            {
                do
                {
                    // receive a single raw packet from the network
                    SerialisedPacket rawRequest = await clientPacketBuffer.Reader.ReadAsync(cancellationToken);

                    if (rawRequest.Equals(SerialisedPacket.Null) ||
                        rawRequest.Type == PacketRegistry.GetPacketId<DisconnectPacket>())
                    {
                        logger.LogMessage(
                            $"Received a disconnect packet from client socket: [Remote EP: {clientEndPoint}]");
                        break;
                    }

                    IRequestPacket? requestPacket = DeserialiseRequestPacket(rawRequest.Type, in rawRequest);
                    Type requestPacketType = PacketRegistry.GetPacketType(rawRequest.Type);

                    // the request packet is only null if no packet handler was registered for it
                    if (requestPacket == default) continue;

                    logger.LogMessage($"Received {rawRequest.Contents.Length} bytes from {clientEndPoint}");

                    logger.LogMessage($"Received request: {Encoding.UTF8.GetString(rawRequest.Contents.Span)}");

                    IResponsePacket<IRequestPacket>? responsePacket =
                        HandleRequestPacket(rawRequest.Type, requestPacket, clientEndPoint);

                    // the response packet is only null if the given request packet was registered as a 'simple' request packet
                    if (responsePacket == default) continue;

                    Type? responsePacketType =
                        PacketRegistry.GetResponsePacketType(requestPacketType);

                    if (responsePacketType == default)
                    {
                        logger.LogError(
                            $"Response packet type for request packet of type {requestPacketType} is null");
                        continue;
                    }

                    SerialisedPacket rawResponse = SerialisedPacket.From(responsePacket);
                    // uint responsePacketTypeId = PacketRegistry.GetPacketId(responsePacketType);
                    // responsePacket.BeforeSerialisation();
                    // SerialisedPacket rawResponse = new SerialisedPacket(responsePacket.Serialise(), responsePacketTypeId);

                    // echo back the processed raw response to the network
                    bool sentCorrectly = await DoSendPacketToAsync(socket, clientEndPoint, rawResponse, SocketFlags.None,
                        NetworkOperationTimeout, cancellationToken);

                    if (!sentCorrectly)
                    {
                        logger.LogWarning(
                            $"Could not send response back to client socket: [Remote EP: {clientEndPoint}]");
                    }
                } while (true);
            }
            finally
            {
                logger.LogMessage($"Stopping client handler for client socket: [Remote EP: {clientEndPoint}]");

                if (activeClients.TryRemove(clientEndPoint, out Channel<SerialisedPacket> packetChannel))
                {
                    packetChannel.Writer.Complete();
                    logger.LogMessage(
                        $"Shutting down packet channel for client socket: [Remote EP: {clientEndPoint}]");
                }
                else
                {
                    logger.LogMessage(
                        $"Couldn't shut down packet channel for client socket: [Remote EP: {clientEndPoint}]");
                }
            }
        }

        /// <inheritdoc />
        public UdpServer(TimeSpan networkOperationTimeout) : base(SocketType.Dgram, ProtocolType.Udp, SocketOptionManager.Udp,
            networkOperationTimeout)
        {
            activeClients = new ConcurrentDictionary<EndPoint, Channel<SerialisedPacket>>();
        }

        /// <inheritdoc />
        public UdpServer() : this(DefaultNetworkOperationTimeout)
        {
        }

        /// <inheritdoc />
        public override async Task RunAsync(EndPoint localEndPoint)
        {
            bool bound = await TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan);

            if (!bound)
            {
                logger.LogError("Server socket was not bound successfully, shutting down server.");
                return;
            }

            OnServerStarted();
            runServer = true;

            while (runServer)
            {
                EndPoint nullEndPoint = new IPEndPoint(IPAddress.Any, 0);
                (SerialisedPacket request, EndPoint remoteEndPoint) =
                    await DoReceivePacketFromAsync(socket, nullEndPoint, SocketFlags.None, Timeout.InfiniteTimeSpan,
                        serverShutdownCancellationToken);
                EndPoint clientEndPoint = remoteEndPoint;

                if (request.Equals(SerialisedPacket.Null))
                {
                    continue;
                }

                if (!activeClients.ContainsKey(clientEndPoint))
                {
                    ClientHandlerArgs args = ClientHandlerArgs.ForUdpClientHandler(in clientEndPoint);

                    activeClients.TryAdd(clientEndPoint, Channel.CreateUnbounded<SerialisedPacket>(clientChannelOptions));

                    await Task.Factory.StartNew(DoHandleClientAsync, args, serverShutdownCancellationToken,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }

                await activeClients[clientEndPoint].Writer.WriteAsync(request);
            }

            OnServerStopped();
        }
    }
}