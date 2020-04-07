using NetSharp.Packets;
using NetSharp.Utils;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Datagram
{
    public class DatagramSocketServer : SocketServer
    {
        private readonly ConcurrentDictionary<EndPoint, RemoteDatagramClientToken> connectedClientTokens;

        private readonly struct RemoteDatagramClientToken
        {
            private readonly Channel<NetworkPacket> PacketChannel;

            public readonly ChannelReader<NetworkPacket> PacketReader;

            public readonly ChannelWriter<NetworkPacket> PacketWriter;

            public RemoteDatagramClientToken(in Channel<NetworkPacket> packetChannel)
            {
                PacketChannel = packetChannel;
                PacketReader = packetChannel.Reader;
                PacketWriter = packetChannel.Writer;
            }
        }

        public DatagramSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, SocketType.Dgram, in connectionProtocolType)
        {
            connectedClientTokens = new ConcurrentDictionary<EndPoint, RemoteDatagramClientToken>();
        }

        protected override SocketAsyncEventArgs GenerateConnectionArgs(EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs { RemoteEndPoint = remoteEndPoint };

            connectionArgs.Completed += SocketAsyncOperations.HandleIoCompleted;

            return connectionArgs;
        }

        protected override void DestroyConnectionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.Completed -= SocketAsyncOperations.HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        protected override async Task HandleClient(SocketAsyncEventArgs clientArgs, CancellationToken cancellationToken = default)
        {
            EndPoint clientEndPoint = clientArgs.RemoteEndPoint;
            RemoteDatagramClientToken clientToken = connectedClientTokens[clientEndPoint];

            byte[] responseBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    NetworkPacket request = await clientToken.PacketReader.ReadAsync(cancellationToken);

                    // TODO implement actual request handling, besides just an echo
                    NetworkPacket response = request;

                    NetworkPacket.Serialise(response, responseBufferMemory);

                    TransmissionResult sendResult =
                        await SocketAsyncOperations
                            .SendToAsync(clientArgs, connection, clientEndPoint, SocketFlags.None, responseBufferMemory, cancellationToken);

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
            byte[] requestBuffer = new byte[NetworkPacket.TotalSize];
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            using SocketAsyncEventArgs remoteArgs = GenerateConnectionArgs(remoteEndPoint);

            while (!cancellationToken.IsCancellationRequested)
            {
                remoteArgs.RemoteEndPoint = remoteEndPoint;

                TransmissionResult receiveResult =
                    await SocketAsyncOperations
                        .ReceiveFromAsync(remoteArgs, connection, remoteEndPoint, SocketFlags.None, requestBufferMemory, cancellationToken)
                        .ConfigureAwait(false);

                EndPoint clientEndPoint = receiveResult.RemoteEndPoint;

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

                if (!ConnectedClientHandlerTasks.ContainsKey(clientEndPoint))
                {
                    SocketAsyncEventArgs clientArgs = GenerateConnectionArgs(receiveResult.RemoteEndPoint);

                    BoundedChannelOptions clientChannelOptions = new BoundedChannelOptions(60)
                        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true };
                    Channel<NetworkPacket> clientChannel = Channel.CreateBounded<NetworkPacket>(clientChannelOptions);

                    connectedClientTokens[clientEndPoint] = new RemoteDatagramClientToken(in clientChannel);

                    ConnectedClientHandlerTasks[clientEndPoint] = HandleClient(clientArgs, cancellationToken);
                }

                NetworkPacket requestPacket = NetworkPacket.Deserialise(requestBufferMemory);

                await connectedClientTokens[clientEndPoint].PacketWriter.WriteAsync(requestPacket, cancellationToken);
            }

            /*
            while (true)
            {
                TransmissionResult receiveResult =
                    await SocketAsyncEventArgs.ReceiveAsync(remoteEndPoint, SocketFlags.None, requestBufferMemory);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

                TransmissionResult sendResult =
                    await SocketAsyncOperations.SendAsync(receiveResult.RemoteEndPoint, SocketFlags.None, requestBuffer);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                }
#endif
            }
            */
        }
    }
}