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
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);

        /// <inheritdoc />
        public string Name => "Raw Datagram Network Writer Benchmark (Asynchronous)";

        private static Task ServerTask(CancellationToken cancellationToken)
        {
            using Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            server.Bind(Program.Constants.ServerEndPoint);

            byte[] transmissionBuffer = new byte[Program.Constants.PacketSize];

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            ServerReadyEvent.Set();

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
            if (Program.Constants.PacketCount > 10_000)
            {
                Console.WriteLine($"{Program.Constants.PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            byte[] sendBuffer = new byte[Program.Constants.PacketSize];
            byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            rawSocket.Bind(Program.Constants.ClientEndPoint);

            using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken) state), serverCts.Token, TaskCreationOptions.LongRunning);

            benchmarkHelper.ResetStopwatch();

            ServerReadyEvent.Wait();

            for (int i = 0; i < Program.Constants.PacketCount; i++)
            {
                byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartStopwatch();
                int sendResult = await writer.WriteAsync(Program.Constants.ServerEndPoint, sendBuffer);

                int receiveResult = await writer.ReadAsync(Program.Constants.ServerEndPoint, receiveBuffer);
                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            benchmarkHelper.PrintBandwidthStats(0, Program.Constants.PacketCount, Program.Constants.PacketSize);
            benchmarkHelper.PrintRttStats(0);

            serverCts.Cancel();

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}