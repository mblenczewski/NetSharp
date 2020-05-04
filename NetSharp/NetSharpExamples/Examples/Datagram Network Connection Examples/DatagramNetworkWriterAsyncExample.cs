using NetSharp.Raw.Datagram;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.Datagram_Network_Connection_Examples
{
    public class DatagramNetworkWriterAsyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;

        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        public static readonly EndPoint ServerEndPoint = DatagramNetworkReaderExample.ServerEndPoint;

        /// <inheritdoc />
        public string Name { get; } = "Datagram Network Writer Example (Asynchronous)";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(ClientEndPoint);

            using DatagramNetworkWriter writer = new DatagramNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);

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