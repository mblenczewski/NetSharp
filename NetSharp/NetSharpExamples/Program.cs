#define TCP
//#undef TCP

using NetSharp.Packets;
using NetSharp.Sockets.Datagram;
using NetSharp.Sockets.Stream;
using NetSharp.Utils;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples
{
    internal class Program
    {
        private const int NetworkTimeout = 1_000_000;
        private const int ServerPort = 12374;
        private static readonly IPAddress ServerAddress = IPAddress.Parse("192.168.0.10");
        private static readonly EndPoint ServerEndPoint = new IPEndPoint(ServerAddress, ServerPort);

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");


            Console.WriteLine("Starting socket server test...");
            Task serverTest = TestSocketServer();

            Console.WriteLine("Starting socket client test...");
            Task clientTest = TestSocketClient();
            await clientTest;

            Console.ReadLine();
        }

        #region Socket Tests

        private static async Task TestSocketClient()
        {
            const int clientCount = 4;
            const long packetsToSend = 100_000;

            Task[] clientTasks = new Task[clientCount];
            double[] clientBandwidths = new double[clientCount];
            HashSet<int> activeTasks = new HashSet<int>(clientCount);

            async Task ClientTask(object id)
            {
                try
                {
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Client {id}] Starting client...");
                    }

#if TCP
                    using StreamSocketClient client = new StreamSocketClient(AddressFamily.InterNetwork, ProtocolType.Tcp);
#else
                    using DatagramSocketClient client = new DatagramSocketClient(AddressFamily.InterNetwork, ProtocolType.Udp);
#endif

                    try
                    {
                        client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    }
                    catch (SocketException)
                    {
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}] Could not bind client socket within timeout!");
                            return;
                        }
                    }
#if TCP
                    client.Connect(in ServerEndPoint);
#endif

                    EndPoint remoteEndPoint = ServerEndPoint;

                    byte[] requestBuffer = new byte[NetworkPacket.TotalSize];
                    Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

                    byte[] responseBuffer = new byte[NetworkPacket.TotalSize];
                    Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                    Stopwatch rttStopwatch = new Stopwatch();
                    Stopwatch bandwidthStopwatch = new Stopwatch();

                    long minRttTicks = int.MaxValue, maxRttTicks = int.MinValue;
                    long minRttMs = int.MaxValue, maxRttMs = int.MinValue;

                    for (int i = 0; i < packetsToSend; i++)
                    {
                        Encoding.UTF8.GetBytes($"[Client {id}] Hello World! (Packet {i})").CopyTo(requestBufferMemory);

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();

#if TCP
                        TransmissionResult sendResult = client.Send(requestBuffer);
#else
                        TransmissionResult sendResult = client.SendTo(remoteEndPoint, requestBuffer);
#endif

                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}, Packet {i}] Sent {sendResult.Count} bytes to {ServerEndPoint}");
                            Console.WriteLine($"[Client {id}, Packet {i}] >>>> {Encoding.UTF8.GetString(requestBufferMemory.Span)}");
                        }
#endif

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();

#if TCP
                        TransmissionResult receiveResult = client.Receive(responseBuffer);
#else
                        TransmissionResult receiveResult = client.ReceiveFrom(ref remoteEndPoint, responseBuffer);
#endif

                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}, Packet {i}] Received {receiveResult.Count} bytes from {remoteEndPoint}");
                            Console.WriteLine($"[Client {id}, Packet {i}] <<<< {Encoding.UTF8.GetString(responseBufferMemory.Span)}");
                        }
#endif

                        lock (typeof(Console))
                        {
                            if (!activeTasks.Contains((int)id))
                            {
                                activeTasks.Add((int)id);
                            }

#if DEBUG
                            Console.WriteLine($"[Client {id}] Client Round Trip Time: {rttStopwatch.ElapsedTicks} ticks ({rttStopwatch.ElapsedMilliseconds} ms)");
#endif
                            minRttTicks = rttStopwatch.ElapsedTicks < minRttTicks
                                ? rttStopwatch.ElapsedTicks
                                : minRttTicks;

                            minRttMs = rttStopwatch.ElapsedMilliseconds < minRttMs
                                ? rttStopwatch.ElapsedMilliseconds
                                : minRttMs;

                            maxRttTicks = rttStopwatch.ElapsedTicks > maxRttTicks
                                ? rttStopwatch.ElapsedTicks
                                : maxRttTicks;

                            maxRttMs = rttStopwatch.ElapsedMilliseconds > maxRttMs
                                ? rttStopwatch.ElapsedMilliseconds
                                : maxRttMs;

                            rttStopwatch.Reset();
                        }
                    }

#if TCP
                    client.Disconnect(true);
                    client.Shutdown(SocketShutdown.Both);
#endif

                    long millis = bandwidthStopwatch.ElapsedMilliseconds;
                    double megabytes = packetsToSend * NetworkPacket.DataSize / 1_000_000.0;
                    double bandwidth = megabytes / (millis / 1000.0);

                    clientBandwidths[(int)id] = bandwidth;

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Client {id}] Sent {packetsToSend} packets to {ServerEndPoint} in {millis} milliseconds");
                        Console.WriteLine($"[Client {id}] Approximate bandwidth: {bandwidth:F3} MBps");

                        Console.WriteLine($"[Client {id}] Min RTT: {minRttTicks} ticks, {minRttMs} ms");
                        Console.WriteLine($"[Client {id}] Max RTT: {maxRttTicks} ticks, {maxRttMs} ms");

                        Console.WriteLine($"[Client {id}] Stopping client...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            }

            for (int clientId = 0; clientId < clientCount; clientId++)
            {
                clientTasks[clientId] = Task.Factory.StartNew(ClientTask, clientId, TaskCreationOptions.LongRunning);
            }

            await Task.WhenAll(clientTasks);

            int totalActiveThreads = 0;
            for (int i = 0; i < clientCount; i++)
            {
                Task clientThread = clientTasks[i];

                bool threadWasActive = activeTasks.Contains(i);
                if (threadWasActive)
                {
                    totalActiveThreads++;
                }

                Console.WriteLine($"[Client task {i}] Approx bandwidth: {clientBandwidths[i]}, was active? {threadWasActive}");
            }

            Console.WriteLine($"Total server bandwidth: {clientBandwidths.Sum():F3} MBps, Total Active Threads: {totalActiveThreads}, Total Inactive Threads: {clientCount - totalActiveThreads}");
        }

        private static async Task TestSocketServer()
        {
            try
            {
#if TCP
                using StreamSocketServer server = new StreamSocketServer(AddressFamily.InterNetwork, ProtocolType.Tcp);
#else
                using DatagramSocketServer server = new DatagramSocketServer(AddressFamily.InterNetwork, ProtocolType.Udp);
#endif

                server.Bind(in ServerEndPoint);

                await server.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        #endregion Socket Tests
    }
}