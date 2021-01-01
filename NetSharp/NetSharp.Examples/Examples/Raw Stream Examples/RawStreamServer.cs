using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Raw;
using NetSharp.Raw.Stream;

namespace NetSharp.Examples.Examples.Raw_Stream_Examples
{
    public class RawStreamServer : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Server";

        /// <inheritdoc />
        public Task RunAsync()
        {
            using RawStreamConnection server = new RawStreamConnection(ProtocolType.Tcp, Constants.DefaultEndPoint);
            server.Bind(Constants.ServerEndPoint);
            Console.WriteLine($"[Server] Bound to {server.LocalEndPoint}");

            static void PacketHandler(
                EndPoint remoteEndPoint,
                in RawPacketHeader header,
                in ReadOnlyMemory<byte> data,
                IRawStreamWriter writer)
            {
                Console.WriteLine(
                    $"[Server] Received {header.DataLength} bytes with type {header.Type} from {remoteEndPoint}");
            }

            server.RegisterHandler(0, PacketHandler);
            server.Start();

            Console.WriteLine("[Server] Started listening for network connections!");
            Console.WriteLine("[Server] Press enter to stop the server...");
            Console.ReadLine();

            Console.WriteLine("[Server] Attempting to stop the server...");
            server.Close();
            Console.WriteLine("[Server] Successfully shut down the server!");

            return Task.CompletedTask;
        }
    }
}
