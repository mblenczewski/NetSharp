using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
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
        /// Holds currently connected and active clients, as well as their current received packet queues.
        /// </summary>
        private readonly ConcurrentDictionary<EndPoint, Channel<Packet>> activeClients;

        /// <summary>
        /// The options that should be applied to every channel created to handle a client.
        /// </summary>
        private static readonly UnboundedChannelOptions clientChannelOptions = new UnboundedChannelOptions
                                                                              {
                                                                                  SingleReader = true,
                                                                                  SingleWriter = true
                                                                              };

        /// <inheritdoc />
        public UdpServer() : base(SocketType.Dgram, ProtocolType.Udp, SocketOptionManager.Udp)
        {
            activeClients = new ConcurrentDictionary<EndPoint, Channel<Packet>>();
        }

        /// <inheritdoc />
        protected override async Task HandleClientAsync(ClientHandlerArgs args)
        {
            EndPoint clientEndPoint = args.ClientEndPoint;
            Channel<Packet> clientPacketBuffer = activeClients[clientEndPoint];

            logger.LogMessage($"Initialised client handler for client socket: [Remote EP: {clientEndPoint}]");

            try
            {
                do
                {
                    // receive a single raw packet from the network
                    Packet rawRequest = await clientPacketBuffer.Reader.ReadAsync(serverShutdownCancellationTokenSource.Token);

                    if (rawRequest.Equals(NullPacket) || rawRequest.Type == PacketRegistry.GetPacketId<DisconnectPacket>())
                    {
                        logger.LogMessage($"Received a disconnect packet from client socket: [Remote EP: {clientEndPoint}]");
                        break;
                    }

                    IRequestPacket? requestPacket = DeserialiseRequestPacket(rawRequest.Type, in rawRequest);
                    Type requestPacketType = PacketRegistry.GetPacketType(rawRequest.Type);

                    // the request packet is only null if no packet handler was registered for it
                    if (requestPacket == null) continue;

                    IResponsePacket<IRequestPacket>? responsePacket =
                        HandleRequestPacket(rawRequest.Type, requestPacket, clientEndPoint);

                    // the response packet is only null if the given request packet was registered as a 'simple' request packet
                    if (responsePacket == null) continue;

                    Type? responsePacketType =
                        PacketRegistry.GetResponsePacketType(requestPacketType);

                    if (responsePacketType == null)
                    {
                        logger.LogError($"Response packet type for request packet of type {requestPacketType} is null");
                        continue;
                    }

                    uint responsePacketTypeId = PacketRegistry.GetPacketId(responsePacketType);

                    responsePacket.BeforeSerialisation();
                    Packet rawResponse = new Packet(responsePacket.Serialise(), responsePacketTypeId, NetworkErrorCode.Ok);

                    // echo back the processed raw response to the network
                    bool sentCorrectly =
                        await DoSendPacketToAsync(socket, clientEndPoint, rawResponse, SocketFlags.None,
                            DefaultNetworkOperationTimeout);

                    if (!sentCorrectly)
                    {
                        logger.LogWarning($"Could not send response back to client socket: [Remote EP: {clientEndPoint}]");
                        break;
                    }
                } while (true);

                logger.LogMessage($"Stopping client handler for client socket: [Remote EP: {clientEndPoint}]");

                if (activeClients.TryRemove(clientEndPoint, out Channel<Packet> packetChannel))
                {
                    packetChannel.Writer.Complete();
                    logger.LogMessage($"Shutting down packet channel for client socket: [Remote EP: {clientEndPoint}]");
                }
                else
                {
                    logger.LogMessage($"Couldn't shut down packet channel for client socket: [Remote EP: {clientEndPoint}]");
                }
            }
            catch (TaskCanceledException) { logger.LogMessage("Client handling was cancelled via a task cancellation."); }
            catch (OperationCanceledException) { logger.LogMessage("Client handling was cancelled via an operation cancellation."); }
            catch (Exception ex)
            {
                logger.LogException("Exception during client socket handling:", ex);
            }
        }

        /// <inheritdoc />
        public override async Task RunAsync(EndPoint localEndPoint)
        {
            bool bound = await TryBindAsync(localEndPoint);

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
                (Packet request, TransmissionResult packetResult) =
                    await DoReceivePacketFromAsync(socket, nullEndPoint, SocketFlags.None, DefaultNetworkOperationTimeout);
                EndPoint clientEndPoint = packetResult.RemoteEndPoint;

                if (request.Equals(NullPacket) || packetResult.Equals(NullTransmissionResult))
                {
                    continue;
                }
                
                if (!activeClients.ContainsKey(clientEndPoint))
                {
                    ClientHandlerArgs args = ClientHandlerArgs.ForUdpClientHandler(in clientEndPoint);

                    activeClients.TryAdd(clientEndPoint, Channel.CreateUnbounded<Packet>(clientChannelOptions));

                    await Task.Factory.StartNew(DoHandleClientAsync, args,
                                   serverShutdownCancellationTokenSource.Token,
                                   TaskCreationOptions.LongRunning,
                                   TaskScheduler.Current);
                }

                await activeClients[clientEndPoint].Writer.WriteAsync(request);
            }

            OnServerStopped();
        }
    }
}