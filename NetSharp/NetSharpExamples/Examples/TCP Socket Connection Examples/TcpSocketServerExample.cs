using NetSharp.Sockets.Stream;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.TCP_Socket_Connection_Examples
{
    public class TcpSocketServerExample : INetSharpExample
    {
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12348);

        /// <inheritdoc />
        public string Name { get; } = "TCP Socket Server Example";

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
            StreamSocketServerOptions serverOptions =
                new StreamSocketServerOptions(Environment.ProcessorCount, 2);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using StreamSocketServer server =
                new StreamSocketServer(ref rawSocket, ServerPacketHandler, serverOptions);

            server.Bind(in ServerEndPoint);

            Console.WriteLine("Starting TCP Socket Server!");

            return server.RunAsync(CancellationToken.None);  // we run forever. alternatively, pass in a cancellation token to ensure that the server terminates
        }
    }
}