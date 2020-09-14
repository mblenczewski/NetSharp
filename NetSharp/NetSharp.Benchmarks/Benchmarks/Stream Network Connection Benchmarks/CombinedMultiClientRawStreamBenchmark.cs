using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Benchmarks.Benchmarks.Stream_Network_Connection_Benchmarks
{
    internal class CombinedMultiClientRawStreamBenchmark : INetSharpBenchmark
    {
        private readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private double[] ClientBandwidths;
        private volatile EndPoint _serverEndPoint = null;

        public string Name => "Combined Raw Stream Network Reader/Writer (Multiple Clients) Benchmark";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            rawSocket.Bind(Program.Constants.DefaultEndPoint);

            try
            {
                int id = (int)idObj;

                EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                byte[] sendBuffer = new byte[Program.Constants.PacketSize];
                byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

                using RawStreamNetworkWriter writer = new RawStreamNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

                benchmarkHelper.ResetStopwatch();

                ServerReadyEvent.Wait();
                EndPoint remoteEndPoint = _serverEndPoint;
                rawSocket.Connect(_serverEndPoint);

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}] Starting client at {rawSocket.LocalEndPoint}; sending messages to {remoteEndPoint}");
                }

                for (int i = 0; i < Program.Constants.PacketCount; i++)
                {
                    byte[] packetBuffer = Program.Constants.ServerEncoding.GetBytes($"[Client {id}] Hello World! (Packet {i})");
                    packetBuffer.CopyTo(sendBuffer, 0);

                    benchmarkHelper.StartStopwatch();
                    int sendResult = writer.Write(remoteEndPoint, sendBuffer);

                    int receiveResult = writer.Read(ref remoteEndPoint, receiveBuffer);
                    benchmarkHelper.StopStopwatch();

                    benchmarkHelper.SnapshotRttStats();
                }

                lock (typeof(Console))
                {
                    benchmarkHelper.PrintBandwidthStats(id, Program.Constants.PacketCount, Program.Constants.PacketSize);
                    benchmarkHelper.PrintRttStats(id);
                }

                ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(Program.Constants.PacketCount, Program.Constants.PacketSize);

                rawSocket.Disconnect(false);
                rawSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client exception: {0}", ex);
            }

            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }

        public async Task RunAsync()
        {
            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            rawSocket.Bind(Program.Constants.DefaultEndPoint);

            _serverEndPoint = rawSocket.LocalEndPoint;

            try
            {
                ClientBandwidths = new double[Program.Constants.ClientCount];
                Task[] clientTasks = new Task[Program.Constants.ClientCount];
                for (int i = 0; i < clientTasks.Length; i++)
                {
                    clientTasks[i] = Task.Factory.StartNew(BenchmarkClientTask, i, TaskCreationOptions.LongRunning);
                }

                EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

                rawSocket.Listen(Program.Constants.ClientCount);

                using RawStreamNetworkReader reader = new RawStreamNetworkReader(ref rawSocket, RequestHandler, defaultEndPoint, Program.Constants.PacketSize);
                reader.Start(Program.Constants.ClientCount);

                ServerReadyEvent.Set();

                await Task.WhenAll(clientTasks);

                Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths.Sum():F3}");

                reader.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server exception: {0}", ex);
            }

            rawSocket.Close();
            rawSocket.Dispose();

            ServerReadyEvent.Reset();
            _serverEndPoint = null;
        }
    }
}