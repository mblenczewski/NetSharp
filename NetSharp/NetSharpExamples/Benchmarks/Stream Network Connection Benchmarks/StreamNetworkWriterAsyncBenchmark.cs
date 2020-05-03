using NetSharp;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Benchmarks.Stream_Network_Connection_Benchmarks
{
    public class StreamNetworkWriterAsyncBenchmark : INetSharpExample, INetSharpBenchmark
    {
        private const int PacketSize = 8192, PacketCount = 100_000;

        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12375);

        public static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim();

        /// <inheritdoc />
        public string Name { get; } = "Stream Network Writer Benchmark (Asynchronous)";

        private static Task ServerTask(CancellationToken cancellationToken)
        {
            using Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            server.Bind(ServerEndPoint);
            ServerReadyEvent.Set();

            byte[] transmissionBuffer = new byte[PacketSize];

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
            if (PacketCount > 10_000)
            {
                Console.WriteLine($"{PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ClientEndPoint);

            using StreamNetworkWriter writer = new StreamNetworkWriter(ref rawSocket, defaultRemoteEndPoint, PacketSize);

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken)state), serverCts.Token, TaskCreationOptions.LongRunning);

            ServerReadyEvent.Wait();
            rawSocket.Connect(ServerEndPoint);

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            byte[] sendBuffer = new byte[PacketSize];
            byte[] receiveBuffer = new byte[PacketSize];

            for (int i = 0; i < PacketCount; i++)
            {
                byte[] packetBuffer = ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartStopwatch();
                int sendResult = await writer.WriteAsync(ServerEndPoint, sendBuffer);

                int receiveResult = await writer.ReadAsync(ServerEndPoint, receiveBuffer);
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
        }
    }
}