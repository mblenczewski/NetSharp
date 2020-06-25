using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    internal class RawDatagramNetworkReaderSingleClientBenchmark : INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 1_000_000;
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private double[] ClientBandwidths;

        /// <inheritdoc />
        public string Name { get; } = "Raw Datagram Network Reader (Single Client) Benchmark";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            int id = (int) idObj;

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            byte[] sendBuffer = new byte[PacketSize];
            byte[] receiveBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

            using Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            clientSocket.Bind(Program.Constants.ClientEndPoint);

            lock (typeof(Console))
            {
                Console.WriteLine($"[Client {id}] Starting client; sending messages to {remoteEndPoint}");
            }

            benchmarkHelper.ResetStopwatch();

            ServerReadyEvent.Wait();

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client {id}] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartStopwatch();
                int sentBytes = clientSocket.SendTo(sendBuffer, remoteEndPoint);

                int receivedBytes = clientSocket.ReceiveFrom(receiveBuffer, ref remoteEndPoint);
                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            lock (typeof(Console))
            {
                benchmarkHelper.PrintBandwidthStats(id, PacketCount, PacketSize);
                benchmarkHelper.PrintRttStats(id);
            }

            ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(PacketCount, PacketSize);

            clientSocket.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            ClientBandwidths = new double[1];
            Task[] clientTasks = new Task[] { Task.Factory.StartNew(BenchmarkClientTask, 0, TaskCreationOptions.LongRunning) };

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            rawSocket.Bind(Program.Constants.ServerEndPoint);

            using RawDatagramNetworkReader reader = new RawDatagramNetworkReader(ref rawSocket, RequestHandler, defaultRemoteEndPoint, PacketSize);
            reader.Start(1);

            ServerReadyEvent.Set();

            await Task.WhenAll(clientTasks);

            Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F3}");

            reader.Shutdown();

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}