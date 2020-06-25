using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    internal class CombinedMultiClientRawDatagramBenchmark : INetSharpBenchmark
    {
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private double[] ClientBandwidths;

        public string Name => "Combined Raw Datagram Network Reader/Writer (Multiple Clients) Benchmark";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

            byte[] sendBuffer = new byte[Program.Constants.PacketSize];
            byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

            EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            rawSocket.Bind(Program.Constants.ClientEndPoint);

            using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

            ServerReadyEvent.Wait();

            benchmarkHelper.ResetStopwatch();

            for (int i = 0; i < Program.Constants.PacketCount; i++)
            {
                byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client 0] Hello World! (Packet {i})");
                packetBuffer.CopyTo(sendBuffer, 0);

                benchmarkHelper.StartStopwatch();
                int sendResult = writer.Write(Program.Constants.ServerEndPoint, sendBuffer);

                int receiveResult = writer.Read(ref remoteEndPoint, receiveBuffer);
                benchmarkHelper.StopStopwatch();

                benchmarkHelper.SnapshotRttStats();
            }

            lock (typeof(Console))
            {
                benchmarkHelper.PrintBandwidthStats(0, Program.Constants.PacketCount, Program.Constants.PacketSize);
                benchmarkHelper.PrintRttStats((int) idObj);
            }

            ClientBandwidths[(int) idObj] = benchmarkHelper.CalcBandwidth(Program.Constants.PacketCount, Program.Constants.PacketSize);

            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }

        public async Task RunAsync()
        {
            if (Program.Constants.PacketCount > 10_000)
            {
                Console.WriteLine($"{Program.Constants.PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            ClientBandwidths = new double[Program.Constants.ClientCount];
            Task[] clientTasks = new Task[Program.Constants.ClientCount];
            for (int i = 0; i < clientTasks.Length; i++)
            {
                clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
            }

            EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            rawSocket.Bind(Program.Constants.ServerEndPoint);

            using RawDatagramNetworkReader reader = new RawDatagramNetworkReader(ref rawSocket, RequestHandler, defaultRemoteEndPoint, Program.Constants.PacketSize);
            reader.Start(Program.Constants.ClientCount);

            ServerReadyEvent.Set();

            await Task.WhenAll(clientTasks);

            Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F3}");

            reader.Shutdown();

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}