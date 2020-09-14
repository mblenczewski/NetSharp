using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Benchmarks.Benchmarks.Stream_Network_Connection_Benchmarks
{
    internal class RawStreamNetworkWriterAsyncBenchmark : INetSharpBenchmark
    {
        private static readonly ManualResetEventSlim ServerReadyEvent = new ManualResetEventSlim(false);

        /// <inheritdoc />
        public string Name => "Raw Stream Network Writer Benchmark (Asynchronous)";

        private static Task ServerTask()
        {
            using Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            rawSocket.Bind(Program.Constants.ServerEndPoint);

            try
            {
                // all the headers should have the same packet size, so will fit in the transmission buffer
                RawStreamPacketHeader archetypalHeader = new RawStreamPacketHeader(Program.Constants.PacketSize);
                byte[] transmissionBuffer = new byte[RawStreamPacket.TotalPacketSize(in archetypalHeader)];

                rawSocket.Listen(1);

                ServerReadyEvent.Set();

                using Socket clientSocket = rawSocket.Accept();
                clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);

                for (int i = 0; i < Program.Constants.PacketCount; i++)
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

                clientSocket.Disconnect(false);
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
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
            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            rawSocket.Bind(Program.Constants.ClientEndPoint);

            try
            {
                EndPoint defaultRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                BenchmarkHelper benchmarkHelper = new BenchmarkHelper();

                byte[] sendBuffer = new byte[Program.Constants.PacketSize];
                byte[] receiveBuffer = new byte[Program.Constants.PacketSize];

                EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

                using RawStreamNetworkWriter writer = new RawStreamNetworkWriter(ref rawSocket, defaultRemoteEndPoint, Program.Constants.PacketSize);

                Task serverTask = Task.Factory.StartNew(ServerTask, TaskCreationOptions.LongRunning);

                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client {0}] Starting client at {rawSocket.LocalEndPoint}; sending messages to {remoteEndPoint}");
                }

                benchmarkHelper.ResetStopwatch();

                ServerReadyEvent.Wait();
                rawSocket.Connect(Program.Constants.ServerEndPoint);

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

                rawSocket.Shutdown(SocketShutdown.Both);
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