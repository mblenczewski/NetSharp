using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    internal class RawDatagramNetworkWriterAsyncBenchmark : INetSharpBenchmark
    {
        private readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private volatile EndPoint _serverEndPoint = null;

        /// <inheritdoc />
        public string Name => "Raw Datagram Network Writer Benchmark (Asynchronous)";

        private Task ServerTask()
        {
            using Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(Program.Constants.DefaultEndPoint);

            _serverEndPoint = rawSocket.LocalEndPoint;

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
            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(Program.Constants.DefaultEndPoint);

            try
            {
                EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                byte[] sendBuffer = new byte[Program.Constants.PacketSize];
                byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

                using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

                Task serverTask = Task.Factory.StartNew(ServerTask, TaskCreationOptions.LongRunning);

                benchmarkHelper.ResetStopwatch();

                ServerReadyEvent.Wait();

                EndPoint remoteEndPoint = _serverEndPoint;

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {0}] Starting client at {rawSocket.LocalEndPoint}; sending messages to {remoteEndPoint}");
                }

                for (int i = 0; i < Program.Constants.PacketCount; i++)
                {
                    byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                    packetBuffer.CopyTo(sendBuffer, 0);

                    benchmarkHelper.StartStopwatch();
                    int sendResult = await writer.WriteAsync(remoteEndPoint, sendBuffer);

                    int receiveResult = await writer.ReadAsync(remoteEndPoint, receiveBuffer);
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

            ServerReadyEvent.Reset();
            _serverEndPoint = null;
        }
    }
}