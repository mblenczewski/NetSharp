using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Provides methods for TCP communication with connected <see cref="TcpClient"/> instances.
    /// </summary>
    public sealed class TcpServer : Server
    {
        /// <inheritdoc />
        protected override async Task HandleClientAsync(ClientHandlerArgs args, CancellationToken cancellationToken)
        {
            Socket clientHandlerSocket = args.ClientSocket ?? new Socket(SocketType.Unknown, ProtocolType.Unknown);
            EndPoint remoteEp = clientHandlerSocket.RemoteEndPoint;

            logger.LogMessage($"Initialised client handler for client socket: [Remote EP: {remoteEp}]");

            try
            {
                do
                {
                    // receive a single raw packet from the network
                    SerialisedPacket rawRequest = SerialisedPacket.Null; //await DoReceivePacketAsync(clientHandlerSocket, SocketFlags.None, Timeout.InfiniteTimeSpan, cancellationToken);

                    if (rawRequest.Equals(SerialisedPacket.Null) ||
                        rawRequest.Type == PacketRegistry.GetPacketId<DisconnectPacket>())
                    {
                        logger.LogMessage(
                            $"Received a disconnect packet from client socket: [Remote EP: {remoteEp}]");
                        break;
                    }

                    IRequestPacket? requestPacket = DeserialiseRequestPacket(rawRequest.Type, in rawRequest);
                    Type requestPacketType = PacketRegistry.GetPacketType(rawRequest.Type);

                    // the request packet is only null if no packet handler was registered for it
                    if (requestPacket == default) continue;

                    logger.LogMessage($"Received {rawRequest.Contents.Length} bytes from {remoteEp}");

                    logger.LogMessage($"Received request: {Encoding.UTF8.GetString(rawRequest.Contents.Span)}");

                    IResponsePacket<IRequestPacket>? responsePacket =
                        HandleRequestPacket(rawRequest.Type, requestPacket, remoteEp);

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
                    bool sentCorrectly = false; //await DoSendPacketAsync(clientHandlerSocket, rawResponse, SocketFlags.None, NetworkOperationTimeout, cancellationToken);

                    if (!sentCorrectly)
                    {
                        logger.LogMessage(
                            $"Could not send response back to client socket: [Remote EP: {remoteEp}]");
                    }

                    logger.LogMessage($"Sent {rawResponse.Contents.Length} bytes to {remoteEp}");
                } while (true);
            }
            finally
            {
                logger.LogMessage($"Stopping client handler for client socket: [Remote EP: {remoteEp}]");
            }
        }

        /// <inheritdoc />
        public TcpServer(TimeSpan networkOperationTimeout) : base(SocketType.Stream, ProtocolType.Tcp, networkOperationTimeout)
        {
        }

        /// <inheritdoc />
        public TcpServer() : this(DefaultNetworkOperationTimeout)
        {
        }

        /// <inheritdoc />
        public override async Task RunAsync(EndPoint localEndPoint)
        {
            bool bound = await TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan);

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

                await Task.Factory.StartNew(DoHandleClientAsync, args, serverShutdownCancellationToken,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            OnServerStopped();
        }
    }
}