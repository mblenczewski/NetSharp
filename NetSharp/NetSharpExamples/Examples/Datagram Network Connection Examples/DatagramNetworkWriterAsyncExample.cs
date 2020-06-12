using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using NetSharp.Raw.Datagram;

namespace NetSharpExamples.Examples.Datagram_Network_Connection_Examples
{
    internal class DatagramNetworkWriterAsyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;
        private static readonly EndPoint ClientEndPoint = Program.DefaultClientEndPoint;
        private static readonly Encoding ServerEncoding = Program.DefaultEncoding;
        private static readonly EndPoint ServerEndPoint = Program.DefaultServerEndPoint;

        /// <inheritdoc />
        public string Name { get; } = "Datagram Network Writer Example (Asynchronous)";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(ClientEndPoint);

            using RawDatagramNetworkWriter writer = new RawDatagramNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);

            byte[] transmissionBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            try
            {
                while (true)
                {
                    Console.Write("Input to send to server: ");
                    string userInput = Console.ReadLine();

                    if (!ServerEncoding.GetBytes(userInput).AsMemory().TryCopyTo(transmissionBuffer))
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
                        Console.WriteLine(ServerEncoding.GetString(transmissionBuffer));
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