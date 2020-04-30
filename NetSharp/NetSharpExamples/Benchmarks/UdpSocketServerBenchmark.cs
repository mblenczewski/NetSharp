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
        /// <summary>
        /// Packets contain 8 KiB of data, so 1 000 000 packet = 8GiB. the more data the more accurate the benchmark, but the slower it will run.
        /// </summary>
        private const int PacketCount = 1_000_000;

        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12347);

        private double[] ClientBandwidths;

        /// <inheritdoc />
        public string Name { get; } = "UDP Socket Server Benchmark";

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

                benchmarkHelper.StartStopwatch();
                int sentBytes = clientSocket.SendTo(sendBuffer, remoteEndPoint);

                int receivedBytes = clientSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            benchmarkHelper.PrintBandwidthStats(id, PacketCount, NetworkPacket.TotalSize);
            benchmarkHelper.PrintRttStats(id);

            ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(PacketCount, NetworkPacket.TotalSize);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            CancellationTokenSource serverCts = new CancellationTokenSource();

            int clientCount = Environment.ProcessorCount / 2;

            Console.WriteLine($"UDP Server Benchmark started!");

            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            DatagramSocketServerOptions serverOptions = new DatagramSocketServerOptions(clientCount, (ushort)clientCount);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            DatagramSocketServer server = new DatagramSocketServer(ref rawSocket, RawSocketServer.DefaultRawPacketHandler, serverOptions);

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

            rawSocket.Close();
            rawSocket.Dispose();

            Console.WriteLine($"UDP Server Benchmark finished!");
        }
    }
}