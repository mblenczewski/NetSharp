using NetSharp.Raw.Stream;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.Stream_Network_Connection_Examples
{
    public class StreamNetworkWriterAsyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;

        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0);

        public static readonly Encoding ServerEncoding = StreamNetworkReaderExample.ServerEncoding;
        public static readonly EndPoint ServerEndPoint = StreamNetworkReaderExample.ServerEndPoint;

        /// <inheritdoc />
        public string Name { get; } = "Stream Network Writer Example (Asynchronous)";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ClientEndPoint);

            using StreamNetworkWriter writer = new StreamNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);
            await writer.ConnectAsync(ServerEndPoint);

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

            await writer.DisconnectAsync(false);

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}