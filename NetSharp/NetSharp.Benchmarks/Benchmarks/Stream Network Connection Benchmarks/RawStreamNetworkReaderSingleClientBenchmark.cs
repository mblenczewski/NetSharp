using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Benchmarks.Benchmarks.Stream_Network_Connection_Benchmarks
{
    internal class RawStreamNetworkReaderSingleClientBenchmark : INetSharpBenchmark
    {
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);
        private double[] ClientBandwidths;

        /// <inheritdoc />
        public string Name => "Raw Stream Network Reader (Single Client) Benchmark";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            return true;
        }

        private Task BenchmarkClientTask(object idObj)
        {
            using Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            clientSocket.Bind(Program.Constants.ClientEndPoint);

            try
            {
                int id = (int) idObj;

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                byte[] sendBuffer = new byte[Program.Constants.PacketSize + RawStreamPacketHeader.TotalSize];
                byte[] receiveBuffer = new byte[Program.Constants.PacketSize + RawStreamPacketHeader.TotalSize];
                byte[] packetBuffer = new byte[Program.Constants.PacketSize];

                EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {id}] Starting client at {clientSocket.LocalEndPoint}; sending messages to {remoteEndPoint}");
                }

                benchmarkHelper.ResetStopwatch();

                ServerReadyEvent.Wait();
                clientSocket.Connect(Program.Constants.ServerEndPoint);

                for (int i = 0; i < Program.Constants.PacketCount; i++)
                {
                    Program.Constants.ServerEncoding.GetBytes($"[Client {id}] Hello World! (Packet {i})").CopyTo(packetBuffer, 0);

                    RawStreamPacketHeader streamPacketHeader = new RawStreamPacketHeader(Program.Constants.PacketSize);
                    RawStreamPacket.Serialise(sendBuffer, in streamPacketHeader, packetBuffer);

                    benchmarkHelper.StartStopwatch();

                    int totalSent = 0;
                    do
                    {
                        totalSent += clientSocket.Send(sendBuffer, totalSent, sendBuffer.Length - totalSent,
                            SocketFlags.None);
                    } while (totalSent != 0 && totalSent < sendBuffer.Length);

                    if (totalSent == 0)
                    {
                        break;
                    }

                    int totalReceived = 0;
                    do
                    {
                        totalReceived += clientSocket.Receive(receiveBuffer, totalReceived, receiveBuffer.Length - totalReceived,
                            SocketFlags.None);
                    } while (totalReceived != 0 && totalReceived < receiveBuffer.Length);

                    if (totalReceived == 0)
                    {
                        break;
                    }

                    benchmarkHelper.StopStopwatch();

                    benchmarkHelper.SnapshotRttStats();
                }

                lock (typeof(Console))
                {
                    benchmarkHelper.PrintBandwidthStats(id, Program.Constants.PacketCount, Program.Constants.PacketSize);
                    benchmarkHelper.PrintRttStats(id);
                }

                ClientBandwidths[id] = benchmarkHelper.CalcBandwidth(Program.Constants.PacketCount, Program.Constants.PacketSize);

                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client exception: {0}", ex);
            }

            clientSocket.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            if (Program.Constants.PacketCount > 10_000)
            {
                Console.WriteLine($"{Program.Constants.PacketCount} packets will be sent per client. This could take a long time (maybe more than a minute)!");
            }

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            rawSocket.Bind(Program.Constants.ServerEndPoint);

            try
            {
                ClientBandwidths = new double[1];
                Task[] clientTasks = new Task[] { Task.Factory.StartNew(BenchmarkClientTask, 0, TaskCreationOptions.LongRunning) };

                EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

                rawSocket.Listen(1);

                using RawStreamNetworkReader reader = new RawStreamNetworkReader(ref rawSocket, RequestHandler, defaultEndPoint, Program.Constants.PacketSize);
                reader.Start(1);

                ServerReadyEvent.Set();

                await clientTasks[0];

                Console.WriteLine($"Total estimated bandwidth: {ClientBandwidths[0]:F3}");

                reader.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Server exception: {0}", ex);
            }

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}