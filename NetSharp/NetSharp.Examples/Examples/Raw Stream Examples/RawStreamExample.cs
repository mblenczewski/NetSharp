using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Raw;
using NetSharp.Raw.Stream;

namespace NetSharp.Examples.Examples.Raw_Stream_Examples
{
    public class RawStreamExample : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Example";

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

            Console.WriteLine("[Server] Started listening for network connections...");

            using RawStreamConnection client = new RawStreamConnection(ProtocolType.Tcp, Constants.DefaultEndPoint);
            client.Bind(Constants.ClientEndPoint);
            Console.WriteLine($"[Client] Bound to {client.LocalEndPoint}");

            client.ConnectAsync(Constants.ServerEndPoint).GetAwaiter().GetResult();
            Console.WriteLine($"[Client] Connected to {client.RemoteEndPoint}");

            byte[] packet = { 42 };
            int sentBytes = client.SendAsync(0, packet).GetAwaiter().GetResult();
            Console.WriteLine($"[Client] Sent {sentBytes} bytes to {client.RemoteEndPoint}");

            client.DisconnectAsync().GetAwaiter().GetResult();
            Console.WriteLine($"[Client] Disconnected from {client.RemoteEndPoint}");

            client.Close();

            server.DeregisterHandler(0, PacketHandler);
            server.Close();

            return Task.CompletedTask;
        }
    }
}
