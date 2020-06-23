using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharp.Examples.Examples.Datagram_Network_Connection_Examples
{
    internal class DatagramNetworkWriterAsyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;

        /// <inheritdoc />
        public string Name { get; } = "Datagram Network Writer Example (Asynchronous)";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(Program.Constants.ClientEndPoint);

            using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);

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

                    int sent = await writer.WriteAsync(remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Sent {sent} bytes to {remoteEndPoint}!");
                    }

                    Array.Clear(transmissionBuffer, 0, transmissionBuffer.Length);

                    int received = await writer.ReadAsync(remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Received {received} bytes from {remoteEndPoint}!");
                        Console.WriteLine(Program.Constants.ServerEncoding.GetString(transmissionBuffer));
                    }
                }
            }
            finally
            {
                rawSocket.Close();
                rawSocket.Dispose();
            }
        }
    }
}