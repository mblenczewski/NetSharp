using NetSharp.Packets;
using NetSharp.Sockets.Stream;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks
{
    public class TcpSocketClientSyncBenchmark : INetSharpExample
    {
        /// <summary>
        /// Packets contain 8 KiB of data, so 1 000 000 packet = 8GiB. the more data the more accurate the benchmark, but the slower it will run.
        /// </summary>
        private const int PacketCount = 1_000_000;

        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12358);

        /// <inheritdoc />
        public string Name { get; } = "TCP Socket Client Benchmark (Synchronous)";

        private Task ServerTask(CancellationToken cancellationToken)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(ServerEndPoint);

            byte[] transmissionBuffer = new byte[NetworkPacket.TotalSize];

            server.Listen(1);
            Socket clientSocket = server.Accept();

            while (!cancellationToken.IsCancellationRequested)
            {
                int expectedBytes = transmissionBuffer.Length;

                int receivedBytes = 0;
                do
                {
                    receivedBytes += clientSocket.Receive(transmissionBuffer, receivedBytes, expectedBytes - receivedBytes, SocketFlags.None);
                } while (receivedBytes != 0 && receivedBytes < expectedBytes);

                if (receivedBytes == 0)
                {
                    break;
                }

                int sentBytes = 0;
                do
                {
                    sentBytes += clientSocket.Send(transmissionBuffer, sentBytes, expectedBytes - sentBytes, SocketFlags.None);
                } while (sentBytes != 0 && sentBytes < expectedBytes);

                if (sentBytes == 0)
                {
                    break;
                }
            }

            server.Shutdown(SocketShutdown.Both);
            server.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            Console.WriteLine($"TCP Client Benchmark started!");

            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken)state), serverCts.Token, TaskCreationOptions.LongRunning);

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            StreamSocketClientOptions clientOptions = new StreamSocketClientOptions((ushort)2);
            using StreamSocketClient client = new StreamSocketClient(AddressFamily.InterNetwork, ProtocolType.Tcp, clientOptions);

            client.Connect(in ServerEndPoint);

            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = Encoding.UTF8.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartBandwidthStopwatch();
                benchmarkHelper.StartRttStopwatch();
                TransmissionResult sendResult = client.Send(sendBuffer);

                TransmissionResult receiveResult = client.Receive(receiveBuffer);
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

            Console.WriteLine($"TCP Client Benchmark finished!");
        }
    }
}