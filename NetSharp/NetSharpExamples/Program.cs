using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSharp;
using NetSharp.Extensions;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharpExamples
{
    internal class Program
    {
        private static int newtorkTimeout = 1_000_000;
        private static IPAddress serverAddress;

        private static int serverPort;

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            serverAddress = IPAddress.Loopback;
            serverPort = 12374;

            await Task.Factory.StartNew(TestServer);
            await Task.Factory.StartNew(TestClient).Result;
        }

        private static async Task TestClient()
        {
            TimeSpan socketTimeout = TimeSpan.FromSeconds(newtorkTimeout);

            const int clientCount = 1;
            const long sentPacketCount = 100;

            EndPoint serverEndPoint = new IPEndPoint(serverAddress, serverPort);
            ConnectionBuilder clientBuilder = new ConnectionBuilder();

            Console.WriteLine($"Testing client connections...");

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async clientId =>
                {
                    Console.WriteLine($"Starting client {clientId}");

                    using Connection client = clientBuilder.Build();
                    await client.TryBindAsync(new IPEndPoint(IPAddress.Any, 0));
                    await client.TryConnectAsync(serverEndPoint);
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
                            int sentBytes = await client.SendAsync(serverEndPoint, requestPacketBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref sentPackets);

                            Console.WriteLine($"[Client {clientId}] Sent {sentBytes} bytes to {serverEndPoint}");

                            stopwatch.Start();
                            //TransmissionResult result = await client.ReceiveFromAsync(serverEndPoint, responseBuffer, SocketFlags.None);
                            TransmissionResult result = await client.ReceiveAsync(serverEndPoint, responseBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref receivedPackets);

                            Console.WriteLine($"[Client {clientId}] Received {result.Count} bytes from {result.RemoteEndPoint}");

                            //await Task.Delay(10);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Client {clientId}] Exception: {ex}");
                        }
                    }

                    long millis = stopwatch.ElapsedMilliseconds;
                    double megabytes = sentPackets * NetworkPacket.DataSegmentSize / 1_000_000.0;

                    Console.WriteLine($"[Client {clientId}] Sent {sentPacketCount} packets to {serverEndPoint} in {millis} milliseconds");
                    Console.WriteLine($"[Client {clientId}] Approximate bandwidth: {megabytes / (millis / 1000.0):F3} MBps");

                    /*
                    Console.WriteLine($"[Client {clientId}] Closing client...");
                    await client.TryDisconnectAsync();
                    Console.WriteLine($"[Client {clientId}] Closed client.");
                    */
                }, i, TaskCreationOptions.LongRunning);
            }

            Console.ReadLine();
        }

        private static async Task TestServer()
        {
            const string serverLogFile = @"./serverLog.txt";
            File.Delete(serverLogFile);
            await using Stream serverOutputStream = File.OpenWrite(serverLogFile);

            EndPoint serverEndPoint = new IPEndPoint(serverAddress, serverPort);
            ConnectionBuilder serverBuilder = new ConnectionBuilder();

            using Connection server = serverBuilder.WithLogging(Console.OpenStandardOutput(), LogLevel.Info).Build();
            await server.TryBindAsync(serverEndPoint);
            //server.SetLoggingStream(Console.OpenStandardOutput());
            //server.ChangeLoggingStream(serverOutputStream, LogLevel.Error);

            Console.WriteLine("Starting server...");

            await server.RunServerAsync();

            Console.WriteLine("Server stopped");

            Console.ReadLine();
        }
    }
}