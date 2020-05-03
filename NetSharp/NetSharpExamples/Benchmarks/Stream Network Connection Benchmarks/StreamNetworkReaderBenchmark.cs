using NetSharp;

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks.Stream_Network_Connection_Benchmarks
{
    public class StreamNetworkReaderBenchmark : INetSharpExample, INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 100_000, ClientCount = 12;

        private double[] ClientBandwidths;
        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12373);

        /// <inheritdoc />
        public string Name { get; } = "Stream Network Reader Benchmark";

        private static bool RequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, Memory<byte> responseBuffer)
        {
            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private Task BenchmarkClientTask(object idObj)
        {
            int id = (int)idObj;

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            clientSocket.Bind(ClientEndPoint);
            clientSocket.Connect(ServerEndPoint);

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

                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            clientSocket.Disconnect(true);
            clientSocket.Close();

            benchmarkHelper.PrintBandwidthStats(id, PacketCount, PacketSize);
            benchmarkHelper.PrintRttStats(id);

            ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(PacketCount, PacketSize);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ServerEndPoint);
            rawSocket.Listen(ClientCount);

            using StreamNetworkReader reader = new StreamNetworkReader(ref rawSocket, RequestHandler, defaultEndPoint, PacketSize);
            reader.Start(ClientCount);

            ClientBandwidths = new double[ClientCount];
            Task[] clientTasks = new Task[ClientCount];
            for (int i = 0; i < clientTasks.Length; i++)
            {
                clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(clientTasks);

            Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F5}");

            reader.Stop();

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}