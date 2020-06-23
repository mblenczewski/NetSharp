using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Examples.Examples.Stream_Network_Connection_Examples
{
    internal class RawStreamChatServer : INetSharpExample
    {
        private static readonly int ChatPacketSize = 8192;
        private static readonly ushort InitialClientCount = 4;
        private static readonly ManualResetEventSlim serverStartedEvent = new ManualResetEventSlim(false);

        public string Name => "Raw Stream Chat Server";

        private static async Task ClientTask()
        {
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientSocket.Bind(Program.Constants.ClientEndPoint);
            clientSocket.Connect(Program.Constants.ServerEndPoint);

            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);
            using RawStreamNetworkWriter client = new RawStreamNetworkWriter(ref clientSocket, defaultEndPoint, ChatPacketSize);

            byte[] transmissionBuffer = new byte[ChatPacketSize];

            try
            {
                while (true)
                {
                    Console.Write("Enter string to send to the server: ");
                    string request = Console.ReadLine();
                    if (!Program.Constants.ServerEncoding.GetBytes(request).AsMemory().TryCopyTo(transmissionBuffer))
                    {
                        Console.WriteLine("Could not copy message to transmission buffer. Please try again!");
                        continue;
                    }

                    int sentBytes = await client.WriteAsync(Program.Constants.ServerEndPoint, transmissionBuffer);

                    Console.WriteLine($"[{clientSocket.LocalEndPoint}] Sent {sentBytes} bytes to server:");
                    Console.WriteLine($"\t{request}");

                    Array.Clear(transmissionBuffer, 0, transmissionBuffer.Length);

                    int receivedBytes = await client.ReadAsync(Program.Constants.ServerEndPoint, transmissionBuffer);

                    string response = Program.Constants.ServerEncoding.GetString(transmissionBuffer);

                    Console.WriteLine($"[{clientSocket.LocalEndPoint}] Received {receivedBytes} bytes from server!");
                    Console.WriteLine($"\t{response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Disconnect(true);

                clientSocket.Close();
                clientSocket.Dispose();
            }
        }

        private static bool ServerPacketHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes, in Memory<byte> responseBuffer)
        {
            lock (typeof(Console))
            {
                string request = Program.Constants.ServerEncoding.GetString(requestBuffer.Span).Trim('\0');

                Console.WriteLine($"[Server] Received request \'{request}\' ({receivedRequestBytes} bytes) from {remoteEndPoint}");
                Console.WriteLine($"[Server] Sending response \'{request}\' ({receivedRequestBytes} bytes) to {remoteEndPoint}");
            }

            return requestBuffer.TryCopyTo(responseBuffer);
        }

        private static Task ServerTask()
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            serverSocket.Bind(Program.Constants.ServerEndPoint);
            serverSocket.Listen(InitialClientCount);

            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);
            using RawStreamNetworkReader server = new RawStreamNetworkReader(ref serverSocket, ServerPacketHandler, defaultEndPoint, ChatPacketSize);

            Console.WriteLine("[Server] Starting server...");

            server.Start(InitialClientCount);

            Console.WriteLine($"[Server] Started up on {Program.Constants.ServerEndPoint}!");
            serverStartedEvent.Set();

            while (true)
            {
            }
        }

        public async Task RunAsync()
        {
            Task serverTask = Task.Factory.StartNew(ServerTask);

            serverStartedEvent.Wait();

            await Task.Factory.StartNew(ClientTask).GetAwaiter().GetResult();
        }
    }
}