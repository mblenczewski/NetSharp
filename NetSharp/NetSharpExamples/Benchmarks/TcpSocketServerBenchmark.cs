using NetSharp.Packets;
using NetSharp.Sockets;
using NetSharp.Sockets.Stream;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks
{
    public class TcpSocketServerBenchmark : INetSharpExample
    {
        /// <summary>
        /// Packets contain 8 KiB of data, so 1 000 000 packet = 8GiB. the more data the more accurate the benchmark, but the slower it will run.
        /// </summary>
        private const int PacketCount = 1_000_000;

        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12348);

        private double[] ClientBandwidths;

        /// <inheritdoc />
        public string Name { get; } = "TCP Socket Server Benchmark";

        private Task BenchmarkClientTask(object idObj)
        {
            int id = (int)idObj;

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            clientSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            clientSocket.Connect(ServerEndPoint);

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

                int totalSent = 0;
                do
                {
                    totalSent += clientSocket.Send(sendBuffer, totalSent, sendBuffer.Length - totalSent,
                        SocketFlags.None);
                } while (totalSent != 0 && totalSent != sendBuffer.Length);

                if (totalSent == 0)
                {
                    break;
                }

                int totalReceived = 0;
                do
                {
                    totalReceived += clientSocket.Receive(receiveBuffer, totalReceived,
                        receiveBuffer.Length - totalReceived, SocketFlags.None);
                } while (totalReceived != 0 && totalReceived != sendBuffer.Length);

                if (totalReceived == 0)
                {
                    break;
                }

                benchmarkHelper.StopRttStopwatch();
                benchmarkHelper.StopBandwidthStopwatch();

                benchmarkHelper.UpdateRttStats(id);
                benchmarkHelper.ResetRttStopwatch();
            }

            clientSocket.Disconnect(true);
            clientSocket.Close();

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

            Console.WriteLine($"TCP Server Benchmark started!");

            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent. This could take a long time (maybe more than a minute)!");
            }

            StreamSocketServerOptions serverOptions = new StreamSocketServerOptions(clientCount, (ushort)clientCount);

            StreamSocketServer server = new StreamSocketServer(AddressFamily.InterNetwork, ProtocolType.Tcp,
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

            Console.WriteLine($"TCP Server Benchmark finished!");
        }
    }
}