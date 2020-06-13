using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    internal class DatagramNetworkWriterSyncBenchmark : INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 1_000_000;
        private static readonly EndPoint ClientEndPoint = Program.DefaultClientEndPoint;
        private static readonly Encoding ServerEncoding = Program.DefaultEncoding;
        private static readonly EndPoint ServerEndPoint = Program.DefaultServerEndPoint;
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim();

        /// <inheritdoc />
        public string Name { get; } = "Raw Datagram Network Writer Benchmark (Synchronous)";

        private static Task ServerTask(CancellationToken cancellationToken)
        {
            using Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            server.Bind(ServerEndPoint);
            ServerReadyEvent.Set();

            byte[] transmissionBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                int received = server.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);

                int sent = server.SendTo(transmissionBuffer, remoteEndPoint);
            }

            server.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(ClientEndPoint);

            using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultRemoteEndPoint, PacketSize);

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken) state), serverCts.Token, TaskCreationOptions.LongRunning);

            ServerReadyEvent.Wait();

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            byte[] sendBuffer = new byte[PacketSize];
            byte[] receiveBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartStopwatch();
                int sendResult = writer.Write(ServerEndPoint, sendBuffer);

                int receiveResult = writer.Read(ref remoteEndPoint, receiveBuffer);
                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            benchmarkHelper.PrintBandwidthStats(0, PacketCount, PacketSize);
            benchmarkHelper.PrintRttStats(0);

            serverCts.Cancel();

            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }
    }
}