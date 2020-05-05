using NetSharp.Raw.Datagram;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.Datagram_Network_Connection_Examples
{
    public class DatagramNetworkReaderExample : INetSharpExample
    {
        private const int PacketSize = 8192, ExpectedClientCount = 8;
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12376);

        /// <inheritdoc />
        public string Name { get; } = "Datagram Network Reader Example";

        private static bool RequestHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes, Memory<byte> responseBuffer)
        {
            requestBuffer.CopyTo(responseBuffer);

            lock (typeof(Console))
            {
                Console.WriteLine($"Received {receivedRequestBytes} bytes from {remoteEndPoint}! Echoing back...");
            }

            return true;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            rawSocket.Bind(ServerEndPoint);

            using RawDatagramNetworkReader reader = new RawDatagramNetworkReader(ref rawSocket, RequestHandler, defaultEndPoint, PacketSize, 100);
            reader.Start(ExpectedClientCount);

            Console.WriteLine($"Started datagram server at {ServerEndPoint}! Enter any key to stop the server...");
            Console.ReadLine();

            reader.Stop();

            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }
    }
}