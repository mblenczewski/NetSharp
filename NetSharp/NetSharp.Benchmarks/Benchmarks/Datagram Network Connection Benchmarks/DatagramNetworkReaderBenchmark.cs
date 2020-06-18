using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using NetSharp.Raw.Datagram;

namespace NetSharp.Benchmarks.Benchmarks.Datagram_Network_Connection_Benchmarks
{
    public class DatagramNetworkReaderBenchmark : INetSharpBenchmark
    {
        private const int ClientCount = 12;
        private const int MaxPacketCount = 1_000_000, MinPacketCount = 100_000, PacketCountInterval = 100_000;
        private const int MaxPacketSize = 65535 / 2, MinPacketSize = 8192;
        private static readonly EndPoint ServerDefaultListenEndpoint = new IPEndPoint(IPAddress.Any, 0);
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private Task[] _clientTasks;
        private Socket _serverSocket;
        private double[] ClientMegabyteBandwidths;

        public ushort BenchmarkClientCount { get; set; } = ClientCount;

        [ParamsSource(nameof(PacketCountParamsSource))]
        public int BenchmarkPacketCount { get; set; }

        [ParamsSource(nameof(PacketSizeParamsSource))]
        public int BenchmarkPacketSize { get; set; }

        /// <inheritdoc />
        public string Name { get; } = "Raw Datagram Network Reader Benchmark";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            int id = (int) idObj;

            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            clientSocket.Bind(Program.Constants.ClientEndPoint);

            ServerReadyEvent.Wait();

            byte[] transmissionBuffer = new byte[BenchmarkPacketSize];

            EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;
            Stopwatch bandwidthStopwatch = new Stopwatch();
            bandwidthStopwatch.Reset();

            for (int i = 0; i < BenchmarkPacketCount; i++)
            {
                byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client {id}] Hello World! (Packet {i})");
                packetBuffer.CopyTo(transmissionBuffer, 0);

                bandwidthStopwatch.Start();
                int sentBytes = clientSocket.SendTo(transmissionBuffer, remoteEndPoint);

                Array.Clear(transmissionBuffer, 0, BenchmarkPacketSize);

                int receivedBytes = clientSocket.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);
                bandwidthStopwatch.Stop();
            }

            ClientMegabyteBandwidths[id] = BenchmarkHelper.CalcBandwidth(bandwidthStopwatch.ElapsedMilliseconds, BenchmarkPacketCount, BenchmarkPacketSize);

            clientSocket.Close();
            clientSocket.Dispose();

            return Task.CompletedTask;
        }

        public static IEnumerable<int> PacketCountParamsSource()
        {
            for (int i = MinPacketCount; i <= MaxPacketCount; i += PacketCountInterval)
            {
                yield return i;
            }
        }

        public static IEnumerable<int> PacketSizeParamsSource()
        {
            for (int i = MinPacketSize; i < MaxPacketSize; i *= 2)
            {
                yield return i;
            }
        }

        [Benchmark(Description = "Measures performance of a Datagram Network Reader with multiple clients")]
        public void MultipleClientPerformance()
        {
            using RawDatagramNetworkReader reader = new RawDatagramNetworkReader(ref _serverSocket, RequestHandler, ServerDefaultListenEndpoint, BenchmarkPacketSize);
            reader.Start(BenchmarkClientCount);

            ServerReadyEvent.Set();

            Task.WhenAll(_clientTasks).GetAwaiter().GetResult();

            reader.Shutdown();
        }

        [IterationCleanup(Target = nameof(MultipleClientPerformance))]
        public void MultipleClientPerformanceCleanup()
        {
            Console.WriteLine($"Total estimated bandwidth: {ClientMegabyteBandwidths.Sum():F3}");

            _serverSocket.Close();
            _serverSocket.Dispose();
        }

        [GlobalCleanup(Target = nameof(MultipleClientPerformance))]
        public void MultipleClientPerformanceGlobalCleanup()
        {
        }

        [GlobalSetup(Target = nameof(MultipleClientPerformance))]
        public void MultipleClientPerformanceGlobalSetup()
        {
        }

        [IterationSetup(Target = nameof(MultipleClientPerformance))]
        public void MultipleClientPerformanceSetup()
        {
            Console.WriteLine($"Packet Size: {BenchmarkPacketSize}, Packet Count: {BenchmarkPacketCount}, Client Count: {BenchmarkClientCount}");

            ServerReadyEvent.Reset();

            ClientMegabyteBandwidths = new double[BenchmarkClientCount];
            _clientTasks = new Task[BenchmarkClientCount];
            for (int i = 0; i < _clientTasks.Length; i++)
            {
                _clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
            }

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _serverSocket.Bind(Program.Constants.ServerEndPoint);
        }

        [Benchmark(Description = "Measures performance of a Datagram Network Reader with a single client")]
        public void SingleClientPerformance()
        {
            using RawDatagramNetworkReader reader = new RawDatagramNetworkReader(ref _serverSocket, RequestHandler, ServerDefaultListenEndpoint, BenchmarkPacketSize);
            reader.Start(BenchmarkClientCount);

            ServerReadyEvent.Set();

            _clientTasks[0].GetAwaiter().GetResult();

            reader.Shutdown();
        }

        [IterationCleanup(Target = nameof(SingleClientPerformance))]
        public void SingleClientPerformanceCleanup()
        {
            Console.WriteLine($"Total estimated bandwidth: {ClientMegabyteBandwidths.Sum():F3}");

            _serverSocket.Close();
            _serverSocket.Dispose();
        }

        [GlobalCleanup(Target = nameof(SingleClientPerformance))]
        public void SingleClientPerformanceGlobalCleanup()
        {
        }

        [GlobalSetup(Target = nameof(SingleClientPerformance))]
        public void SingleClientPerformanceGlobalSetup()
        {
        }

        [IterationSetup(Target = nameof(SingleClientPerformance))]
        public void SingleClientPerformanceSetup()
        {
            Console.WriteLine($"Packet Size: {BenchmarkPacketSize}, Packet Count: {BenchmarkPacketCount}, Client Count: {BenchmarkClientCount}");

            ServerReadyEvent.Reset();

            ClientMegabyteBandwidths = new double[BenchmarkClientCount];
            _clientTasks = new Task[] { Task.Factory.StartNew(BenchmarkClientTask, 0, TaskCreationOptions.LongRunning) };

            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _serverSocket.Bind(Program.Constants.ServerEndPoint);
        }
    }
}