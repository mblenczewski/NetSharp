#define TCP
//#undef TCP

using NetSharp.Sockets.Datagram;
using NetSharp.Sockets.Stream;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NetworkPacket = NetSharp.Packets.NetworkPacket;
using SocketServer = NetSharp.Sockets.SocketServer;

namespace NetSharpExamples
{
    internal class Program
    {
        private const int NetworkTimeout = 1_000_000;
        private const int ServerPort = 12374;
        private static readonly IPAddress ServerAddress = IPAddress.Parse("192.168.0.15");
        private static readonly EndPoint ServerEndPoint = new IPEndPoint(ServerAddress, ServerPort);

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            await Task.Factory.StartNew(TestSocketServer);
            await Task.Factory.StartNew(TestSocketClient).Result;

            Console.ReadLine();
        }

        #region Socket Tests

        private static async Task TestSocketClient()
        {
            const int clientCount = 16;
            const long packetsToSend = 100_000;

            Thread[] clientThreads = new Thread[clientCount];
            double[] clientBandwidths = new double[clientCount];
            HashSet<int> activeThreads = new HashSet<int>(clientCount);

            async void ClientTask(object id)
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
                        Encoding.UTF8.GetBytes($"Hello World! (Packet {i})").CopyTo(requestBufferMemory);

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();
#if TCP
                        int sendResult = client.SendBytes(requestBufferMemory);
#else
                        int sendResult = client.SendBytesTo(requestBufferMemory, ServerEndPoint);
#endif

                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}, Packet {i}] Sent {sendResult} bytes to {ServerEndPoint}");
                            Console.WriteLine($"[Client {id}, Packet {i}] >>>> {Encoding.UTF8.GetString(requestBufferMemory.Span)}");
                        }
#endif

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();
                        EndPoint serverEndPoint = ServerEndPoint;

#if TCP
                        int receiveResult = client.ReceiveBytes(responseBufferMemory);
#else
                        int receiveResult = client.ReceiveBytesFrom(responseBufferMemory, ref serverEndPoint);
#endif

                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}, Packet {i}] Received {receiveResult} bytes from {serverEndPoint}");
                            Console.WriteLine($"[Client {id}, Packet {i}] <<<< {Encoding.UTF8.GetString(responseBufferMemory.Span)}");
                        }
#endif

                        lock (typeof(Console))
                        {
                            if (!activeThreads.Contains((int)id))
                            {
                                activeThreads.Add((int)id);
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
                    client.Disconnect();
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
                clientThreads[clientId] = new Thread(ClientTask) { Name = $"Client thread {clientId:D3}" };
                clientThreads[clientId].Start(clientId);
            }

            Console.ReadLine();

            int totalActiveThreads = 0;
            for (int i = 0; i < clientCount; i++)
            {
                Thread clientThread = clientThreads[i];

                bool threadWasActive = activeThreads.Contains(i);
                if (threadWasActive)
                {
                    totalActiveThreads++;
                }

                Console.WriteLine($"{clientThread.Name} bandwidth: {clientBandwidths[i]}, was active? {threadWasActive}");
            }

            Console.WriteLine($"Total server bandwidth: {clientBandwidths.Sum():F3} MBps, Total Active Threads: {totalActiveThreads}, Total Inactive Threads: {clientCount - totalActiveThreads}");
        }

        private static async Task TestSocketServer()
        {
#if TCP
            using SocketServer server = new StreamSocketServer(AddressFamily.InterNetwork, ProtocolType.Tcp);
#else
            using SocketServer server = new DatagramSocketServer(AddressFamily.InterNetwork, ProtocolType.Udp);
#endif

            server.Bind(in ServerEndPoint);

            await server.RunAsync();
        }

#endregion Socket Tests
    }
}