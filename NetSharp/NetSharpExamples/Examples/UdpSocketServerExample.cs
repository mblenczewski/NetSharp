using NetSharp.Packets;
using NetSharp.Sockets.Datagram;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples
{
    public class UdpSocketServerExample : INetSharpExample
    {
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12347);

        /// <inheritdoc />
        public string Name { get; } = "UDP Socket Server Example";

        public static bool ServerPacketHandler(in EndPoint remoteEndPoint, ReadOnlyMemory<byte> request, Memory<byte> response)
        {
            // lock is not necessary, but means that console output is clean and not interleaved
            lock (typeof(Console))
            {
                Console.WriteLine($"[Server] Received request with contents \'{ServerEncoding.GetString(request.Span).TrimEnd('\0', ' ')}\' from {remoteEndPoint}");
                Console.WriteLine($"[Server] Echoing back request to {remoteEndPoint}");
            }

            // we echo back the request, but we could just as easily send back a new packet. if we would not want to send back any response, we need
            // to return false

            request.CopyTo(response);

            return true;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            DatagramSocketServerOptions serverOptions =
                new DatagramSocketServerOptions(Environment.ProcessorCount, 2);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            using DatagramSocketServer server =
                new DatagramSocketServer(ref rawSocket, ServerPacketHandler, serverOptions);

            server.Bind(in ServerEndPoint);

            Console.WriteLine("Starting UDP Socket Server!");

            return server.RunAsync(CancellationToken.None);  // we run forever. alternatively, pass in a cancellation token to ensure that the server terminates
        }
    }
}