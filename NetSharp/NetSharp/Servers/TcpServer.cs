using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Interfaces;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;
using NetSharp.Utils.Socket_Options;

namespace NetSharp.Servers
{
    /// <summary>
    /// Provides methods for TCP communication with connected <see cref="Clients.TcpClient"/> instances.
    /// </summary>
    public sealed class TcpServer : Server
    {
        /// <inheritdoc />
        public TcpServer() : base(SocketType.Stream, ProtocolType.Tcp, SocketOptionManager.Tcp)
        {
        }

        /// <inheritdoc />
        protected override async Task HandleClientAsync(ClientHandlerArgs args)
        {
            Socket clientHandlerSocket = args.ClientSocket ?? new Socket(SocketType.Unknown, ProtocolType.Unknown);

            EndPoint remoteEp = clientHandlerSocket.RemoteEndPoint;

            logger.LogMessage($"Initialised client handler for client socket: [Remote EP: {remoteEp}]");

            try
            {
                do
                {
                    // receive a single raw packet from the network
                    Packet rawRequest =
                        await DoReceivePacketAsync(clientHandlerSocket, SocketFlags.None, Timeout.InfiniteTimeSpan);

                    if (rawRequest.Equals(NullPacket) || rawRequest.Type == PacketRegistry.GetPacketId<DisconnectPacket>())
                    {
                        logger.LogMessage($"Received a disconnect packet from client socket: [Remote EP: {remoteEp}]");
                        break;
                    }

                    IRequestPacket? requestPacket = DeserialiseRequestPacket(rawRequest.Type, in rawRequest);
                    Type requestPacketType = PacketRegistry.GetPacketType(rawRequest.Type);

                    // the request packet is only null if no packet handler was registered for it
                    if (requestPacket == null) continue;

                    logger.LogMessage($"Received {rawRequest.Count} bytes from {remoteEp}");

                    logger.LogMessage($"Received request: {Encoding.UTF8.GetString(rawRequest.Buffer.Span)}");

                    IResponsePacket<IRequestPacket>? responsePacket =
                        HandleRequestPacket(rawRequest.Type, requestPacket, remoteEp);

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
                        await DoSendPacketAsync(clientHandlerSocket, rawResponse, SocketFlags.None, DefaultNetworkOperationTimeout);

                    if (!sentCorrectly)
                    {
                        logger.LogMessage($"Could not send response back to client socket: [Remote EP: {remoteEp}]");
                        break;
                    }

                    logger.LogMessage($"Sent {rawResponse.TotalSize} bytes to {remoteEp}");
                } while (true);

                logger.LogMessage($"Stopping client handler for client socket: [Remote EP: {remoteEp}]");
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

            logger.LogMessage($"Is server socket bound: {bound}");

            if (!bound)
            {
                logger.LogError("Server socket was not bound successfully, shutting down server.");
                return;
            }

            socket.Listen(PendingConnectionBacklog);

            OnServerStarted();
            runServer = true;

            while (runServer)
            {
                Socket clientSocket = await socket.AcceptAsync();
                ClientHandlerArgs args = ClientHandlerArgs.ForTcpClientHandler(in clientSocket);

                await Task.Factory.StartNew(DoHandleClientAsync, args,
                    serverShutdownCancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Current);
            }

            OnServerStopped();
        }
    }
}