using NetSharp.Packets;
using NetSharp.Sockets.Datagram;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.UDP_Socket_Connection_Examples
{
    public class UdpSocketClientExample : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "UDP Socket Client Example";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            DatagramSocketClientOptions clientOptions = new DatagramSocketClientOptions(2);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            using DatagramSocketClient client = new DatagramSocketClient(ref rawSocket, clientOptions);

            Encoding dataEncoding = UdpSocketServerExample.ServerEncoding;
            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = UdpSocketServerExample.ServerEndPoint;

            Console.WriteLine("Starting UDP Socket Client!");

            for (int i = 0; i < 10; i++)
            {
                string data = $"Hello World from {client.LocalEndPoint}!";
                dataEncoding.GetBytes(data).CopyTo(sendBuffer, 0);

                TransmissionResult sendResult = client.Send(in remoteEndPoint, sendBuffer, SocketFlags.None);

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult sendResult =
                    await client.SendAsync(in remoteEndPoint, sendBuffer, SocketFlags.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Sent request with contents \'{data}\' to {remoteEndPoint}");
                }

                TransmissionResult receiveResult = client.Receive(in remoteEndPoint, receiveBuffer, SocketFlags.None);
                remoteEndPoint = receiveResult.RemoteEndPoint;

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult receiveResult =
                    await client.ReceiveAsync(in remoteEndPoint, receiveBuffer, SocketFlags.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Received response with contents \'{dataEncoding.GetString(receiveBuffer).TrimEnd('\0', ' ')}\' from {remoteEndPoint}");
                }
            }

            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}