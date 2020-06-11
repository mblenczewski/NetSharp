using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharpExamples.Benchmarks.Stream_Network_Connection_Benchmarks
{
    internal class StreamNetworkWriterSyncBenchmark : INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 1_000_000;

        public static readonly EndPoint ClientEndPoint = Program.DefaultClientEndPoint;
        public static readonly Encoding ServerEncoding = Program.DefaultEncoding;
        public static readonly EndPoint ServerEndPoint = Program.DefaultServerEndPoint;

        public static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim();

        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Network Writer Benchmark (Synchronous)";

        private static Task ServerTask(CancellationToken cancellationToken)
        {
            using Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(ServerEndPoint);
            ServerReadyEvent.Set();

            // all the headers should have the same packet size, so will fit in the transmission buffer
            RawStreamPacketHeader archetypalHeader = new RawStreamPacketHeader(PacketSize);
            byte[] transmissionBuffer = new byte[RawStreamPacket.TotalPacketSize(in archetypalHeader)];

            server.Listen(1);
            Socket clientSocket = server.Accept();

            while (!cancellationToken.IsCancellationRequested)
            {
                int expectedBytes = transmissionBuffer.Length;

                int receivedBytes = 0;
                do
                {
                    receivedBytes += clientSocket.Receive(transmissionBuffer, receivedBytes, expectedBytes - receivedBytes, SocketFlags.None);
                } while (receivedBytes < expectedBytes && receivedBytes > 0);

                if (receivedBytes == 0)
                {
                    break;
                }

                int sentBytes = 0;
                do
                {
                    sentBytes += clientSocket.Send(transmissionBuffer, sentBytes, expectedBytes - sentBytes, SocketFlags.None);
                } while (sentBytes < expectedBytes && sentBytes > 0);

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
        public Task RunAsync()
        {
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ClientEndPoint);

            using RawStreamNetworkWriter writer = new RawStreamNetworkWriter(ref rawSocket, defaultRemoteEndPoint, PacketSize);

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken) state), serverCts.Token, TaskCreationOptions.LongRunning);

            ServerReadyEvent.Wait();
            rawSocket.Connect(ServerEndPoint);

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
            try
            {
                serverTask.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            rawSocket.Disconnect(false);
            rawSocket.Shutdown(SocketShutdown.Both);
            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }
    }
}