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

        public static readonly EndPoint ClientEndPoint = Program.DefaultClientEndPoint;
        public static readonly Encoding ServerEncoding = Program.DefaultEncoding;
        public static readonly EndPoint ServerEndPoint = Program.DefaultServerEndPoint;

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

            while (true)
            {
                int sent = await writer.WriteAsync(remoteEndPoint, transmissionBuffer);

                lock (typeof(Console))
                {
                    Console.WriteLine($"Sent {sent} bytes to {remoteEndPoint}!");
                }

                int received = await writer.ReadAsync(remoteEndPoint, transmissionBuffer);

                lock (typeof(Console))
                {
                    Console.WriteLine($"Received {received} bytes from {remoteEndPoint}!");
                }
            }

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}