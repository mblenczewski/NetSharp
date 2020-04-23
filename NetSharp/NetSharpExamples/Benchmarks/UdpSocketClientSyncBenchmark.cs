using NetSharp.Packets;
using NetSharp.Sockets.Datagram;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks
{
    public class UdpSocketClientSyncBenchmark : INetSharpExample
    {
        /// <summary>
        /// Packets contain 8 KiB of data, so 1 000 000 packet = 8GiB. the more data the more accurate the benchmark, but the slower it will run.
        /// </summary>
        private const int PacketCount = 1_000_000;

        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12357);

        /// <inheritdoc />
        public string Name { get; } = "UDP Socket Client Benchmark (Synchronous)";

        private Task ServerTask(CancellationToken cancellationToken)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            server.Bind(ServerEndPoint);

            byte[] transmissionBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                server.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);

                server.SendTo(transmissionBuffer, remoteEndPoint);
            }

            server.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            Console.WriteLine($"UDP Client Benchmark started!");

            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken)state), serverCts.Token, TaskCreationOptions.LongRunning);

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            DatagramSocketClientOptions clientOptions = new DatagramSocketClientOptions((ushort)2);
            using DatagramSocketClient client = new DatagramSocketClient(AddressFamily.InterNetwork, ProtocolType.Udp, clientOptions);

            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = Encoding.UTF8.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartBandwidthStopwatch();
                benchmarkHelper.StartRttStopwatch();
                TransmissionResult sendResult = client.SendTo(remoteEndPoint, sendBuffer);

                TransmissionResult receiveResult = client.ReceiveFrom(ref remoteEndPoint, receiveBuffer);
                benchmarkHelper.StopRttStopwatch();
                benchmarkHelper.StopBandwidthStopwatch();

                benchmarkHelper.UpdateRttStats(0);
                benchmarkHelper.ResetRttStopwatch();
            }

            benchmarkHelper.PrintBandwidthStats(0, PacketCount, NetworkPacket.TotalSize);
            benchmarkHelper.PrintRttStats(0);

            serverCts.Cancel();
            try
            {
                serverTask.Dispose();
            }
            catch (Exception) { }

            Console.WriteLine($"UDP Client Benchmark finished!");
        }
    }
}