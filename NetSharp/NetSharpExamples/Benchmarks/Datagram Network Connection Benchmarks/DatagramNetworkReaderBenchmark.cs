using NetSharp.Raw.Datagram;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    public class DatagramNetworkReaderBenchmark : INetSharpExample, INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 1_000_000, ClientCount = 12;

        private double[] ClientBandwidths;
        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12370);

        public static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim();

        /// <inheritdoc />
        public string Name { get; } = "Datagram Network Reader Benchmark";

        private static bool RequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes, Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            try
            {
                int id = (int)idObj;

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                clientSocket.Bind(ClientEndPoint);

                ServerReadyEvent.Wait();

                byte[] sendBuffer = new byte[PacketSize];
                byte[] receiveBuffer = new byte[PacketSize];

                EndPoint remoteEndPoint = ServerEndPoint;

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}] Starting client; sending messages to {remoteEndPoint}");
                }

                for (int i = 0; i < PacketCount; i++)
                {
                    byte[] packetBuffer = ServerEncoding.GetBytes($"[Client {id}] Hello World! (Packet {i})");
                    packetBuffer.CopyTo(sendBuffer, 0);

                    benchmarkHelper.StartStopwatch();
                    int sentBytes = clientSocket.SendTo(sendBuffer, remoteEndPoint);

                    int receivedBytes = clientSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                    benchmarkHelper.StopStopwatch();

                    benchmarkHelper.SnapshotRttStats();
                }

                benchmarkHelper.PrintBandwidthStats(id, PacketCount, PacketSize);
                benchmarkHelper.PrintRttStats(id);

                ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(PacketCount, PacketSize);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            ClientBandwidths = new double[ClientCount];
            Task[] clientTasks = new Task[ClientCount];
            for (int i = 0; i < clientTasks.Length; i++)
            {
                clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(ServerEndPoint);

            using DatagramNetworkReader reader = new DatagramNetworkReader(ref rawSocket, RequestHandler, defaultRemoteEndPoint, PacketSize);
            reader.Start(ClientCount);

            ServerReadyEvent.Set();

            await Task.WhenAll(clientTasks);

            Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F3}");

            reader.Stop();

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}