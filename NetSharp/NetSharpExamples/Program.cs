using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NetSharp;
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

            Console.WriteLine("Test server (y/n): ");
            if (Console.ReadLine()?.ToLower().Equals("y") ?? false)
            {
                await TestServer();

                Console.ReadLine();
            }
            else
            {
                await TestClient();

                Console.ReadLine();
            }
        }

        private static async Task TestClient()
        {
            TimeSpan socketTimeout = TimeSpan.FromSeconds(newtorkTimeout);

            const int clientCount = 10;
            const int sentPacketCount = 1_000_000;

            EndPoint serverEndPoint = new IPEndPoint(serverAddress, serverPort);

            static Connection ClientFactory()
            {
                return ConnectionFactory.InitUdp(new IPEndPoint(IPAddress.Loopback, 0));
            }

            Console.WriteLine($"Testing client connections...");

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async clientId =>
                {
                    Console.WriteLine($"Starting client {clientId}");

                    using Connection client = ClientFactory();
                    TimeSpan timeout = TimeSpan.FromMilliseconds(1);

                    byte[] message = Encoding.UTF8.GetBytes("Hello World!");
                    Memory<byte> messageBuffer = new Memory<byte>(message);

                    byte[] response = new byte[NetworkPacket.PacketSize];
                    Memory<byte> responseBuffer = new Memory<byte>(response);

                    for (int j = 0; j < sentPacketCount; j++)
                    {
                        int sentBytes = await client.SendToAsync(serverEndPoint, messageBuffer, SocketFlags.None, timeout);

                        Console.WriteLine($"[Client {clientId}] Sent {sentBytes} bytes to {serverEndPoint}");

                        TransmissionResult result = await client.ReceiveFromAsync(serverEndPoint, responseBuffer, SocketFlags.None, timeout);

                        Console.WriteLine($"[Client {clientId}] Received {result.Count} bytes from {result.RemoteEndPoint}");

                        //await Task.Delay(10);
                    }
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

            using Connection server = ConnectionFactory.InitUdp(serverEndPoint);
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

                Console.WriteLine($"[Server] Received {result.Count} bytes from {result.RemoteEndPoint}");

                int sentBytes = await server.SendToAsync(result.RemoteEndPoint, requestBuffer, SocketFlags.None);

                Console.WriteLine($"[Server] Sent {sentBytes} bytes tp {result.RemoteEndPoint}");
            }

            Console.WriteLine("Server stopped");

            Console.ReadLine();
        }
    }
}