using NetSharp.Packets;
using NetSharp.Sockets.Stream;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples
{
    public class TcpSocketServerExample : INetSharpExample
    {
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12348);

        public static NetworkPacket ServerPacketHandler(in NetworkPacket request, in EndPoint remoteEndPoint)
        {
            // lock is not necessary, but means that console output is clean and not interleaved
            lock (typeof(Console))
            {
                Console.WriteLine($"[Server] Received request with contents \'{ServerEncoding.GetString(request.Data.Span)}\' from {remoteEndPoint}");
                Console.WriteLine($"[Server] Echoing back request to {remoteEndPoint}");
            }

            // we echo back the request, but we could just as easily send back a new packet. if we would not want to send back any response, we need
            // to return NetworkPacket.NullPacket
            return request;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            StreamSocketServerOptions serverOptions =
                new StreamSocketServerOptions(Environment.ProcessorCount, 2);

            using StreamSocketServer server =
                new StreamSocketServer(AddressFamily.InterNetwork, ProtocolType.Tcp, ServerPacketHandler, serverOptions);

            server.Bind(in ServerEndPoint);

            return server.RunAsync(CancellationToken.None);  // we run forever. alternatively, pass in a cancellation token to ensure that the server terminates
        }
    }
}