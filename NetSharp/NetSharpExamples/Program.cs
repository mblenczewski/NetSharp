using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSharp;
using NetSharp.Extensions;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Sockets;
using NetSharp.Utils;

namespace NetSharpExamples
{
    internal class Program
    {
        private const int NetworkTimeout = 1_000_000;
        private const int ServerPort = 12374;
        private static readonly IPAddress ServerAddress = IPAddress.Parse("10.4.3.167"); // IPAddress.Parse("192.168.0.31");
        private static readonly EndPoint ServerEndPoint = new IPEndPoint(ServerAddress, ServerPort);

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            await Task.Factory.StartNew(TestSocketServer);
            await Task.Factory.StartNew(TestSocketClient).Result;

            Console.ReadLine();

            //await Task.Factory.StartNew(TestServer);
            //await Task.Factory.StartNew(TestClient).Result;
        }

        #region Connection Tests

        private static async Task TestClient()
        {
            TimeSpan socketTimeout = TimeSpan.FromSeconds(NetworkTimeout);

            const int clientCount = 10;
            const long sentPacketCount = 10_000;

            ConnectionBuilder clientBuilder = new ConnectionBuilder();

            Console.WriteLine($"Testing client connections...");

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async clientId =>
                {
                    Console.WriteLine($"Starting client {clientId}");

                    using Connection client = clientBuilder.Build();
                    await client.TryBindAsync(new IPEndPoint(IPAddress.Any, 0));
                    await client.TryConnectAsync(ServerEndPoint);
                    //client.SetLoggingStream(Console.OpenStandardOutput());
                    //TimeSpan timeout = TimeSpan.FromMilliseconds(100);
                    Stopwatch stopwatch = new Stopwatch();

                    byte[] message = Encoding.UTF8.GetBytes("Hello World!");
                    Memory<byte> messageBuffer = new Memory<byte>(message);

                    NetworkPacket requestPacket = new NetworkPacket(messageBuffer, message.Length, 1, NetworkErrorCode.Ok, false);
                    byte[] requestPacketBuffer = new byte[NetworkPacket.PacketSize];
                    NetworkPacket.SerialiseToBuffer(requestPacketBuffer, requestPacket);

                    byte[] response = new byte[NetworkPacket.PacketSize];
                    Memory<byte> responseBuffer = new Memory<byte>(response);

                    long sentPackets = 0, receivedPackets = 0;
                    for (int j = 0; j < sentPacketCount; j++)
                    {
                        try
                        {
                            stopwatch.Start();
                            //int sentBytes = await client.SendToAsync(serverEndPoint, requestPacketBuffer, SocketFlags.None);
                            int sentBytes = await client.SendAsync(ServerEndPoint, requestPacketBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref sentPackets);

                            //Console.WriteLine($"[Client {clientId}] Sent {sentBytes} bytes to {serverEndPoint}");

                            stopwatch.Start();
                            //TransmissionResult result = await client.ReceiveFromAsync(serverEndPoint, responseBuffer, SocketFlags.None);
                            TransmissionResult result = await client.ReceiveAsync(ServerEndPoint, responseBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref receivedPackets);

                            //Console.WriteLine($"[Client {clientId}] Received {result.Count} bytes from {result.RemoteEndPoint}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Client {clientId}] Exception: {ex}");
                        }
                    }

                    long millis = stopwatch.ElapsedMilliseconds;
                    double megabytes = sentPackets * NetworkPacket.DataSegmentSize / 1_000_000.0;

                    Console.WriteLine($"[Client {clientId}] Sent {sentPacketCount} packets to {ServerEndPoint} in {millis} milliseconds");
                    Console.WriteLine($"[Client {clientId}] Approximate bandwidth: {megabytes / (millis / 1000.0):F3} MBps");

                    await client.TryDisconnectAsync();
                    Console.WriteLine($"[Client {clientId}] Closed client.");
                }, i, TaskCreationOptions.LongRunning);
            }

            Console.ReadLine();
        }

        private static async Task TestServer()
        {
            const string serverLogFile = @"./serverLog.txt";
            File.Delete(serverLogFile);
            await using Stream serverOutputStream = File.OpenWrite(serverLogFile);

            ConnectionBuilder serverBuilder = new ConnectionBuilder();

            using Connection server = serverBuilder.WithLogging(Console.OpenStandardOutput(), LogLevel.Info).Build();
            await server.TryBindAsync(ServerEndPoint);
            //server.SetLoggingStream(Console.OpenStandardOutput());
            //server.ChangeLoggingStream(serverOutputStream, LogLevel.Error);

            Console.WriteLine("Starting server...");

            await server.RunServerAsync();

            Console.WriteLine("Server stopped");

            Console.ReadLine();
        }

        #endregion Connection Tests

        #region Socket Tests

        private static async Task TestSocketClient()
        {
            const int clientCount = 100;
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

                    using SocketClient client = new SocketClient(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    if (!await client.TryBindAsync(new IPEndPoint(IPAddress.Any, 0), TimeSpan.FromMilliseconds(1_000)))
                    {
                        lock (typeof(Console))
                        {
                            Console.WriteLine($"[Client {id}] Could not bind client socket within timeout!");
                            return;
                        }
                    }

                    byte[] requestBuffer = new byte[NetworkPacket.PacketSize];
                    Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

                    byte[] responseBuffer = new byte[NetworkPacket.PacketSize];
                    Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                    Stopwatch rttStopwatch = new Stopwatch();
                    Stopwatch bandwidthStopwatch = new Stopwatch();

                    for (int i = 0; i < packetsToSend; i++)
                    {
                        Encoding.UTF8.GetBytes($"Hello World! (Packet {i})").CopyTo(requestBufferMemory);

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();
                        TransmissionResult sendResult = await client.SendAsync(ServerEndPoint, SocketFlags.None, requestBufferMemory);
                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            //Console.WriteLine($"[Client {id}, Packet {i}] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                            //Console.WriteLine($"[Client {id}, Packet {i}] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                        }
#endif

                        rttStopwatch.Start();
                        bandwidthStopwatch.Start();
                        TransmissionResult receiveResult = await client.ReceiveAsync(ServerEndPoint, SocketFlags.None, responseBufferMemory);
                        bandwidthStopwatch.Stop();
                        rttStopwatch.Stop();

#if DEBUG
                        lock (typeof(Console))
                        {
                            //Console.WriteLine($"[Client {id}, Packet {i}] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                            //Console.WriteLine($"[Client {id}, Packet {i}] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                        }
#endif

                        lock (typeof(Console))
                        {
                            if (!activeThreads.Contains((int)id))
                            {
                                activeThreads.Add((int)id);
                            }

                            //Console.WriteLine($"[Client {id}] Client Round Trip Time: {rttStopwatch.ElapsedMilliseconds} ms");
                            rttStopwatch.Reset();
                        }
                    }

                    long millis = bandwidthStopwatch.ElapsedMilliseconds;
                    double megabytes = packetsToSend * NetworkPacket.DataSegmentSize / 1_000_000.0;
                    double bandwidth = megabytes / (millis / 1000.0);

                    clientBandwidths[(int)id] = bandwidth;

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Client {id}] Sent {packetsToSend} packets to {ServerEndPoint} in {millis} milliseconds");
                        Console.WriteLine($"[Client {id}] Approximate bandwidth: {bandwidth:F3} MBps");

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
            using SocketServer server =
                new SocketServer(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            await server.TryBindAsync(ServerEndPoint, TimeSpan.FromMilliseconds(1_000));

            byte[] requestBuffer = new byte[NetworkPacket.PacketSize];
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            while (true)
            {
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                TransmissionResult receiveResult =
                    await server.ReceiveAsync(remoteEndPoint, SocketFlags.None, requestBufferMemory);

                TransmissionResult sendResult =
                    await server.SendAsync(receiveResult.RemoteEndPoint, SocketFlags.None, requestBuffer);
            }
        }

        #endregion Socket Tests
    }
}