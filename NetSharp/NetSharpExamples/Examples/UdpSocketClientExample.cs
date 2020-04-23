using NetSharp.Packets;
using NetSharp.Sockets.Datagram;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples
{
    public class UdpSocketClientExample : INetSharpExample
    {
        /// <inheritdoc />
        public async Task RunAsync()
        {
            DatagramSocketClientOptions clientOptions = new DatagramSocketClientOptions(2);

            using DatagramSocketClient client = new DatagramSocketClient(AddressFamily.InterNetwork, ProtocolType.Udp, clientOptions);

            Encoding dataEncoding = UdpSocketServerExample.ServerEncoding;
            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = UdpSocketServerExample.ServerEndPoint;

            while (true)
            {
                string data = $"Hello World from {client.LocalEndPoint}!";
                dataEncoding.GetBytes(data).CopyTo(sendBuffer, 0);

                TransmissionResult sendResult =
                    client.SendTo(remoteEndPoint, sendBuffer, SocketFlags.None);

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult sendResult =
                    await client.SendToAsync(remoteEndPoint, sendBuffer, SocketFlags.None, CancellationToken.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Sent request with contents \'{data}\' to {remoteEndPoint}");
                }

                TransmissionResult receiveResult =
                    client.ReceiveFrom(ref remoteEndPoint, receiveBuffer, SocketFlags.None);

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult receiveResult =
                    await client.ReceiveFromAsync(remoteEndPoint, receiveBuffer, SocketFlags.None, CancellationToken.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Received response with contents \'{dataEncoding.GetString(receiveBuffer)}\' from {remoteEndPoint}");
                }
            }
        }
    }
}