using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    internal class RawDatagramNetworkWriterSyncBenchmark : INetSharpBenchmark
    {
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);

        /// <inheritdoc />
        public string Name => "Raw Datagram Network Writer Benchmark (Synchronous)";

        private static Task ServerTask()
        {
            using Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(Program.Constants.ServerEndPoint);

            try
            {
                byte[] transmissionBuffer = new byte[Program.Constants.PacketSize];

                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                ServerReadyEvent.Set();

                for (int i = 0; i < Program.Constants.PacketCount; i++)
                {
                    rawSocket.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);

                    rawSocket.SendTo(transmissionBuffer, remoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server exception: {0}", ex);
            }

            rawSocket.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            if (Program.Constants.PacketCount > 10_000)
            {
                Console.WriteLine($"{Program.Constants.PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(Program.Constants.ClientEndPoint);

            try
            {
                EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                byte[] sendBuffer = new byte[Program.Constants.PacketSize];
                byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

                EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

                using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

                Task serverTask = Task.Factory.StartNew(ServerTask, TaskCreationOptions.LongRunning);

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {0}] Starting client at {rawSocket.LocalEndPoint}; sending messages to {remoteEndPoint}");
                }

                benchmarkHelper.ResetStopwatch();

                ServerReadyEvent.Wait();

                for (int i = 0; i < Program.Constants.PacketCount; i++)
                {
                    byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                    packetBuffer.CopyTo(sendBuffer, 0);

                    benchmarkHelper.StartStopwatch();
                    int sendResult = writer.Write(remoteEndPoint, sendBuffer);

                    int receiveResult = writer.Read(ref remoteEndPoint, receiveBuffer);
                    benchmarkHelper.StopStopwatch();

                    benchmarkHelper.SnapshotRttStats();
                }

                benchmarkHelper.PrintBandwidthStats(0, Program.Constants.PacketCount, Program.Constants.PacketSize);
                benchmarkHelper.PrintRttStats(0);

                serverTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client exception: {0}", ex);
            }

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}