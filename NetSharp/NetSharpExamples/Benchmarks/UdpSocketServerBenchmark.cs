using NetSharp.Packets;
using NetSharp.Sockets;
using NetSharp.Sockets.Datagram;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks
{
    public class UdpSocketServerBenchmark : INetSharpExample
    {
        private const int PacketCount = 1_000_000;

        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12347);

        private double[] ClientBandwidths;

        /// <inheritdoc />
        public async Task RunAsync()
        {
            CancellationTokenSource serverCts = new CancellationTokenSource();

            int clientCount = Environment.ProcessorCount / 2;

            Console.WriteLine($"UDP Server Benchmark started!");

            DatagramSocketServerOptions serverOptions = new DatagramSocketServerOptions(NetworkPacket.TotalSize,
                clientCount, (ushort)clientCount);

            DatagramSocketServer server = new DatagramSocketServer(AddressFamily.InterNetwork, ProtocolType.Udp,
                SocketServer.DefaultPacketHandler, serverOptions);

            server.Bind(ServerEndPoint);

            Task serverTask = Task.Factory.StartNew(() =>
            {
                server.RunAsync(serverCts.Token).GetAwaiter().GetResult();
            }, TaskCreationOptions.LongRunning);

            ClientBandwidths = new double[clientCount];
            Task[] clientTasks = new Task[clientCount];
            for (int i = 0; i < clientTasks.Length; i++)
            {
                clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(clientTasks);

            Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F5}");

            serverCts.Cancel();

            await serverTask;

            Console.WriteLine($"UDP Server Benchmark finished!");
        }

        private Task BenchmarkClientTask(object idObj)
        {
            int id = (int)idObj;

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            clientSocket.Bind(new IPEndPoint(IPAddress.Any, 0));

            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {id}] Starting client; sending messages to {remoteEndPoint}");
            }

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = Encoding.UTF8.GetBytes($"[Client {id}] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartBandwidthStopwatch();
                benchmarkHelper.StartRttStopwatch();
                int sentBytes = clientSocket.SendTo(sendBuffer, remoteEndPoint);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}, Packet {i}] Sent {sentBytes} bytes to {remoteEndPoint}");
                    Console.WriteLine($"[Client {id}, Packet {i}] >>>> {Encoding.UTF8.GetString(sendBuffer)}");
                }
#endif

                int receivedBytes = clientSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                benchmarkHelper.StopRttStopwatch();
                benchmarkHelper.StopBandwidthStopwatch();

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}, Packet {i}] Received {receivedBytes} bytes from {remoteEndPoint}");
                    Console.WriteLine($"[Client {id}, Packet {i}] <<<< {Encoding.UTF8.GetString(receiveBuffer)}");
                }
#endif

                benchmarkHelper.UpdateRttStats(id);

                /*
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}] Current RTT: {benchmarkHelper.RttTicks} ticks, {benchmarkHelper.RttMs} ms");
                }
                */

                benchmarkHelper.ResetRttStopwatch();
            }

            benchmarkHelper.PrintBandwidthStats(id, PacketCount, NetworkPacket.TotalSize);
            benchmarkHelper.PrintRttStats(id);

            ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(PacketCount, NetworkPacket.TotalSize);

            return Task.CompletedTask;
        }
    }
}