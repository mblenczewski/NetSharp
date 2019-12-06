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
using NetSharp.Servers;

namespace NetSharpExamples
{
    internal class Program
    {
        private static IPAddress serverAddress;

        private static int serverPort;

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            //await TestTaskCancellation();

            //TestThreadCancellation();

            //TestManualResetEvent();

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
            TimeSpan socketTimeout = Timeout.InfiniteTimeSpan; //TimeSpan.FromSeconds(10);

            const int clientCount = 1;
            const int sentPacketCount = 1_000_000;

            static Client ClientFactory()
            {
                return new TcpClient();
            }

            for (int i = 0; i < clientCount; i++)
            {
                await Task.Factory.StartNew(async () =>
                {
                    using Client client = ClientFactory();
                    client.ChangeLoggingStream(Console.OpenStandardOutput());

                    if (await client.TryBindAsync(null, null, socketTimeout))
                    {
                        Console.WriteLine($"Socket bound successfully: {client.SocketOptions.LocalIPEndPoint}");

                        if (await client.TryConnectAsync(serverAddress, serverPort, socketTimeout))
                        {
                            Console.WriteLine(
                                $"Socket connected successfully: {client.SocketOptions.RemoteIPEndPoint}, sending messages to server...");

                            //var message = new SimpleRequestPacket { Message = "Hello World" };
                            byte[] message = Encoding.UTF8.GetBytes("Hello World!");

                            Stopwatch messageStopwatch = Stopwatch.StartNew();

                            for (int j = 0; j < sentPacketCount; j++)
                            {
                                await client.SendBytesAsync(message);
                                //Console.WriteLine($"[{j}] Sent message to server.");

                                //byte[] response = await client.SendBytesWithResponseAsync(message);
                                //Console.WriteLine($"[{j}] Received response: {Encoding.UTF8.GetString(response)}");

                                //await Task.Delay(new Random(DateTime.Now.Millisecond).Next(200, 500));
                            }

                            messageStopwatch.Stop();

                            Console.WriteLine(
                                $"Sending {sentPacketCount} packets of {message.Length} bytes long took {messageStopwatch.Elapsed}");

                            double bandwidth = message.Length * sentPacketCount / (messageStopwatch.ElapsedMilliseconds / 1000.0);

                            Console.WriteLine($"Approximate bandwidth for single connection is {bandwidth} Bytes per second");

                            client.Disconnect();
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
                }, TaskCreationOptions.LongRunning);
            }

            Console.ReadLine();
        }

        private static void TestManualResetEvent()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            ManualResetEventSlim reset = new ManualResetEventSlim(false);

            Thread waitThread = new Thread(tokenObj =>
            {
                CancellationToken token = (CancellationToken)tokenObj;

                try
                {
                    Console.WriteLine("Waiting for manual reset event to be signaled.");
                    reset.Wait(token);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while waiting: {ex}");
                }
            });

            waitThread.Start(cts.Token);

            Thread.Sleep(100);

            reset.Set();

            waitThread.Join();
        }

        private static async Task TestServer()
        {
            const string serverLogFile = @"./serverLog.txt";
            File.Delete(serverLogFile);
            await using Stream serverOutputStream = File.OpenWrite(serverLogFile);

            using Server server = new TcpServer();
            //server.ChangeLoggingStream(Console.OpenStandardOutput());
            //server.ChangeLoggingStream(serverOutputStream);

            Console.WriteLine("Starting server...");
            await server.RunAsync(serverAddress, serverPort);
            Console.WriteLine("Server stopped");

            Console.ReadLine();

            serverOutputStream.Close();
        }

        private static async Task TestTaskCancellation()
        {
            using CancellationTokenSource _cts = new CancellationTokenSource();

            // simulates heavy thread work with a timeout value.
            async Task<bool> ProcessTask(CancellationTokenSource cts, int taskProcessTimeMs, int timeoutMs)
            {
                try
                {
                    int threadSleep = taskProcessTimeMs, timeout = timeoutMs;

                    Console.WriteLine($"Task process time ms: {threadSleep}, task timeout ms: {timeout}");

                    cts.CancelAfter(timeout);

                    bool wasCancelled = await Task.Run(() =>
                    {
                        Thread.Sleep(threadSleep);
                        return true;
                    }, cts.Token);

                    Console.WriteLine($"Was task successful: {wasCancelled}");

                    return wasCancelled;
                }
                catch (TaskCanceledException ex)
                {
                    Console.WriteLine($"Cancellation Token cancelled during task execution: {ex.Message}");

                    return false;
                }
            }

            await ProcessTask(_cts, 50, 100);

            await ProcessTask(_cts, 1000, 100);
        }

        private static void TestThreadCancellation()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();

            Thread processingThread = new Thread(tokenObj =>
            {
                CancellationToken token = (CancellationToken)tokenObj;

                try
                {
                    Console.WriteLine("Simulating heavy work and unresponsive thread...");

                    while (true)
                    {
                        // simulate very heave work

                        token.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException ex)
                {
                    Console.WriteLine($"Cancellation token was cancelled during task: {ex.Message}");
                }
            });

            cts.CancelAfter(1000);

            processingThread.Start(cts.Token);

            processingThread.Join();
        }
    }
}