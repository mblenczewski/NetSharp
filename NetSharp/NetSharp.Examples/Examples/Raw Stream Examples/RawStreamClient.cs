using System;
using System.Net.Sockets;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharp.Examples.Examples.Raw_Stream_Examples
{
    public class RawStreamClient : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Client";

        /// <inheritdoc />
        public Task RunAsync()
        {
            using RawStreamConnection client = new RawStreamConnection(ProtocolType.Tcp, Constants.DefaultEndPoint);
            client.Bind(Constants.ClientEndPoint);
            Console.WriteLine($"[Client] Bound to {client.LocalEndPoint}");

            client.ConnectAsync(Constants.ServerEndPoint).GetAwaiter().GetResult();
            Console.WriteLine($"[Client] Connected to {client.RemoteEndPoint}");

            byte[] packet = new byte[Constants.PacketSize];
            byte[] message = Constants.ServerEncoding.GetBytes("Hello World!");

            int sentBytes;
            do
            {
                message.CopyTo(packet, 0);

                sentBytes = client.SendAsync(0, packet).GetAwaiter().GetResult();
                Console.WriteLine($"[Client] Sent {sentBytes} bytes to {client.RemoteEndPoint}");
            } while (sentBytes > 0);

            client.DisconnectAsync().GetAwaiter().GetResult();
            Console.WriteLine($"[Client] Disconnected from {client.RemoteEndPoint}");

            client.Close();

            return Task.CompletedTask;
        }
    }
}
