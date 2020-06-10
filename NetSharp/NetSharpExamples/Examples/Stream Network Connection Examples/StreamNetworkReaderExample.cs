using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharpExamples.Examples.Stream_Network_Connection_Examples
{
    public class StreamNetworkReaderExample : INetSharpExample
    {
        private const int PacketSize = 8192, ExpectedClientCount = 8;
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12377);

        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Network Reader Example";

        private static bool RequestHandler(EndPoint remoteEndPoint, in ReadOnlyMemory<byte> requestBuffer, int receivedRequestBytes,
            in Memory<byte> responseBuffer)
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

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ServerEndPoint);
            rawSocket.Listen(ExpectedClientCount);

            using RawStreamNetworkReader reader = new RawStreamNetworkReader(ref rawSocket, RequestHandler, defaultEndPoint, PacketSize, 100);
            reader.Start(ExpectedClientCount);

            Console.WriteLine($"Started stream server at {ServerEndPoint}! Enter any key to stop the server...");
            Console.ReadLine();

            reader.Stop();

            rawSocket.Close();
            rawSocket.Dispose();

            return Task.CompletedTask;
        }
    }
}