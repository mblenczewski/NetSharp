using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetSharp;
using NetSharp.Clients;
using NetSharp.Extensions;
using NetSharp.Logging;
using NetSharp.Servers;

namespace NetSharpExamples
{
    internal class Program
    {
        private static IPAddress serverAddress;

        private static int serverPort;

        private static int newtorkTimeout = 1_000_000;

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            serverAddress = IPAddress.Loopback;
            serverPort = 12374;

            Console.WriteLine("Test server (y/n): ");
            if (Console.ReadLine()?.ToLower().Equals("y") ?? false)
            {
                try
                {
                    await TestServer();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex}");
                }

                Console.ReadLine();
            }
            else
            {
                try
                {
                    await TestClient();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex}");
                }

                Console.ReadLine();
            }
        }

        private static async Task TestClient()
        {
            TimeSpan socketTimeout = TimeSpan.FromSeconds(newtorkTimeout);

            const int clientCount = 1;
            const int sentPacketCount = 1_000_000;

            static Client ClientFactory()
            {
                return new UdpClient();
            }

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async () =>
                {
                    using Client client = ClientFactory();

                    if (await client.TryBindAsync(null, null, socketTimeout))
                    {
                        Console.WriteLine($"Socket bound successfully: {client.SocketOptions.LocalIPEndPoint}");

                        if (await client.TryConnectAsync(serverAddress, serverPort, socketTimeout))
                        {
                            Console.WriteLine(
                                $"Socket connected successfully: {client.SocketOptions.RemoteIPEndPoint}, sending messages to server...");

                            //var message = new SimpleRequestPacket { Message = "Hello World" };
                            byte[] message = Encoding.UTF8.GetBytes("Hello World!");
                            bool failedToSend = false;

                            Stopwatch messageStopwatch = Stopwatch.StartNew();

                            for (int j = 0; j < sentPacketCount; j++)
                            {
                                if (!await client.SendBytesAsync(message, socketTimeout))
                                {
                                    failedToSend = true;
                                    break;
                                }

                                //Console.WriteLine($"[{j}] Sent message to server.");

                                //byte[] response = await client.SendBytesWithResponseAsync(message);
                                //Console.WriteLine($"[{j}] Received response: {Encoding.UTF8.GetString(response)}");

                                //await Task.Delay(new Random(DateTime.Now.Millisecond).Next(200, 500));
                            }

                            messageStopwatch.Stop();

                            if (!failedToSend)
                            {
                                Console.WriteLine(
                                    $"Sending {sentPacketCount} packets of {message.Length} bytes long took {messageStopwatch.Elapsed}");

                                double bandwidth = message.Length * sentPacketCount / (messageStopwatch.ElapsedMilliseconds / 1000.0);

                                Console.WriteLine($"Approximate bandwidth for single connection is {bandwidth} Bytes per second");
                            }
                            else
                            {
                                Console.WriteLine("Could not successfully send all packets to server.");
                            }

                            client.Disconnect();
                            Console.WriteLine($"Sent disconnect packet to server.");
                        }
                        else
                        {
                            Console.WriteLine("Socket could not connect");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Socket could not be bound");
                    }
                }, TaskCreationOptions.LongRunning).Result;
            }

            Console.ReadLine();
        }

        private static async Task TestServer()
        {
            const string serverLogFile = @"./serverLog.txt";
            File.Delete(serverLogFile);
            await using Stream serverOutputStream = File.OpenWrite(serverLogFile);

            using Server server = new UdpServer();
            server.ChangeLoggingStream(Console.OpenStandardOutput(), LogLevel.Warn);
            //server.ChangeLoggingStream(serverOutputStream, LogLevel.Error);

            Console.WriteLine("Starting server...");
            await server.RunAsync(serverAddress, serverPort);
            Console.WriteLine("Server stopped");

            Console.ReadLine();

            serverOutputStream.Close();
        }
    }
}