using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Examples.Examples.Stream_Network_Connection_Examples
{
    internal class StreamNetworkWriterSyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;

        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Network Writer Example (Synchronous)";

        /// <inheritdoc />
        public Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(Program.Constants.ClientEndPoint);
            rawSocket.Connect(Program.Constants.ServerEndPoint);

            using RawStreamNetworkWriter writer = new RawStreamNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);

            byte[] transmissionBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = Program.Constants.ServerEndPoint;

            try
            {
                while (true)
                {
                    Console.Write("Input to send to server: ");
                    string userInput = Console.ReadLine();

                    if (!Program.Constants.ServerEncoding.GetBytes(userInput).AsMemory().TryCopyTo(transmissionBuffer))
                    {
                        Console.WriteLine("Given input is too large. Please try again!");
                        continue;
                    }

                    int sent = writer.Write(remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Sent {sent} bytes to {remoteEndPoint}!");
                    }

                    Array.Clear(transmissionBuffer, 0, transmissionBuffer.Length);

                    int received = writer.Read(ref remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Received {received} bytes from {remoteEndPoint}!");
                        Console.WriteLine(Program.Constants.ServerEncoding.GetString(transmissionBuffer));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                rawSocket.Shutdown(SocketShutdown.Both);
                rawSocket.Disconnect(true);

                rawSocket.Close();
                rawSocket.Dispose();
            }

            return Task.CompletedTask;
        }
    }
}