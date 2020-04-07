using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharp.Sockets.Stream
{
    public class StreamSocketServer : SocketServer
    {
        private readonly ConcurrentDictionary<EndPoint, RemoteStreamClientToken> connectedClientTokens;

        private readonly struct RemoteStreamClientToken
        {
            private readonly Channel<NetworkPacket> PacketChannel;

            public readonly ChannelReader<NetworkPacket> PacketReader;

            public readonly ChannelWriter<NetworkPacket> PacketWriter;

            public RemoteStreamClientToken(in Channel<NetworkPacket> packetChannel)
            {
                PacketChannel = packetChannel;
                PacketReader = packetChannel.Reader;
                PacketWriter = packetChannel.Writer;
            }
        }

        public StreamSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Stream, in connectionProtocolType)
        {
            connectedClientTokens = new ConcurrentDictionary<EndPoint, RemoteStreamClientToken>();
        }

        protected override SocketAsyncEventArgs GenerateConnectionArgs(EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs { RemoteEndPoint = remoteEndPoint };

            connectionArgs.Completed += SocketAsyncOperations.HandleIoCompleted;

            return connectionArgs;
        }

        protected override void DestroyConnectionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            remoteConnectionArgs.AcceptSocket.Close();

            remoteConnectionArgs.Completed -= SocketAsyncOperations.HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        protected override async Task HandleClient(SocketAsyncEventArgs clientArgs, CancellationToken cancellationToken = default)
        {
            EndPoint clientEndPoint = clientArgs.AcceptSocket.RemoteEndPoint;
            RemoteStreamClientToken clientToken = connectedClientTokens[clientEndPoint];

            Socket clientSocket = clientArgs.AcceptSocket;

            byte[] requestBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            byte[] responseBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    TransmissionResult receiveResult =
                        await SocketAsyncOperations
                            .ReceiveAsync(clientArgs, clientSocket, clientEndPoint, SocketFlags.None, requestBufferMemory, cancellationToken)
                            .ConfigureAwait(false);

                    if (receiveResult.Count == 0)
                    {
                        break;
                    }
#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                    }
#endif

                    NetworkPacket request = NetworkPacket.Deserialise(requestBufferMemory);

                    // TODO implement actual request handling, besides just an echo
                    NetworkPacket response = request;

                    NetworkPacket.Serialise(response, responseBufferMemory);

                    TransmissionResult sendResult =
                        await SocketAsyncOperations
                            .SendAsync(clientArgs, clientSocket, clientEndPoint, SocketFlags.None, responseBufferMemory, cancellationToken)
                            .ConfigureAwait(false);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                DestroyConnectionArgs(clientArgs);
            }
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            connection.Listen(100);

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                SocketAsyncEventArgs clientArgs = GenerateConnectionArgs(remoteEndPoint);

                await SocketAsyncOperations.AcceptAsync(clientArgs, connection, cancellationToken);

                EndPoint clientEndPoint = clientArgs.AcceptSocket.RemoteEndPoint;

                BoundedChannelOptions clientChannelOptions = new BoundedChannelOptions(60) 
                    { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true };
                Channel<NetworkPacket> clientChannel = Channel.CreateBounded<NetworkPacket>(clientChannelOptions);

                connectedClientTokens[clientEndPoint] = new RemoteStreamClientToken(in clientChannel);

                ConnectedClientHandlerTasks[clientEndPoint] = HandleClient(clientArgs, cancellationToken);
            }
        }
    }
}