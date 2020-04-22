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
        public Task RunAsync()
        {
            DatagramSocketServerOptions serverOptions =
                new DatagramSocketServerOptions(NetworkPacket.TotalSize, Environment.ProcessorCount, 2);

            using DatagramSocketServer server =
                new DatagramSocketServer(AddressFamily.InterNetwork, ProtocolType.Udp, ServerPacketHandler, serverOptions);

            return server.RunAsync(CancellationToken.None);  // we run forever. alternatively, pass in a cancellation token to ensure that the server terminates
        }

        public static NetworkPacket ServerPacketHandler(in NetworkPacket request, in EndPoint remoteEndPoint)
        {
            // lock is not necessary, but means that console output is clean and not interleaved
            lock (typeof(Console))
            {
                Console.WriteLine($"[Server] Received request with contents \'{Encoding.UTF8.GetString(request.Data.Span)}\' from {remoteEndPoint}");
                Console.WriteLine($"[Server] Echoing back request to {remoteEndPoint}");
            }

            // we echo back the request, but we could just as easily send back a new packet.
            // if we would not want to send back any response, we need to return NetworkPacket.NullPacket
            return request;
        }
    }
}