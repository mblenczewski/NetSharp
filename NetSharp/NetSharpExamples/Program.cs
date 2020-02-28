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

            const int clientCount = 10;
            const long sentPacketCount = 10_000;

            EndPoint serverEndPoint = new IPEndPoint(serverAddress, serverPort);
            ConnectionBuilder clientBuilder = new ConnectionBuilder().WithUdp();

            Connection ClientFactory()
            {
                return clientBuilder.Build();
            }

            Console.WriteLine($"Testing client connections...");

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async clientId =>
                {
                    Console.WriteLine($"Starting client {clientId}");

                    using Connection client = ClientFactory();
                    client.TryBind(new IPEndPoint(IPAddress.Any, 0));
                    //TimeSpan timeout = TimeSpan.FromMilliseconds(100);
                    Stopwatch stopwatch = new Stopwatch();

                    byte[] message = Encoding.UTF8.GetBytes("Hello World!");
                    Memory<byte> messageBuffer = new Memory<byte>(message);

                    byte[] response = new byte[NetworkPacket.PacketSize];
                    Memory<byte> responseBuffer = new Memory<byte>(response);

                    long sentPackets = 0, receivedPackets = 0;
                    for (int j = 0; j < sentPacketCount; j++)
                    {
                        try
                        {
                            stopwatch.Start();
                            int sentBytes = await client.SendToAsync(serverEndPoint, messageBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref sentPackets);

                            //Console.WriteLine($"[Client {clientId}] Sent {sentBytes} bytes to {serverEndPoint}");

                            stopwatch.Start();
                            TransmissionResult result = await client.ReceiveFromAsync(serverEndPoint, responseBuffer, SocketFlags.None);
                            stopwatch.Stop();
                            Interlocked.Increment(ref receivedPackets);

                            //Console.WriteLine($"[Client {clientId}] Received {result.Count} bytes from {result.RemoteEndPoint}");

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
            ConnectionBuilder serverBuilder = new ConnectionBuilder().WithUdp();

            using Connection server = serverBuilder.Build();
            server.TryBind(serverEndPoint);
            //server.SetLoggingStream(Console.OpenStandardOutput(), LogLevel.Info);
            //server.ChangeLoggingStream(serverOutputStream, LogLevel.Error);

            Console.WriteLine("Starting server...");

            EndPoint nullEndPoint = new IPEndPoint(IPAddress.Any, 0);

            byte[] request = new byte[NetworkPacket.PacketSize];
            Memory<byte> requestBuffer = new Memory<byte>(request);

            while (true)
            {
                TransmissionResult result =
                    await server.ReceiveFromAsync(nullEndPoint, requestBuffer, SocketFlags.None);

                //Console.WriteLine($"[Server] Received {result.Count} bytes from {result.RemoteEndPoint}");

                int sentBytes = await server.SendToAsync(result.RemoteEndPoint, requestBuffer, SocketFlags.None);

                //Console.WriteLine($"[Server] Sent {sentBytes} bytes tp {result.RemoteEndPoint}");
            }

            Console.WriteLine("Server stopped");

            Console.ReadLine();
        }
    }
}