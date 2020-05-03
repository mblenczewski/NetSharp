using NetSharp.Packets;
using NetSharp.Sockets.Stream;
using NetSharp.Utils;

using NetSharpExamples.Examples.UDP_Socket_Connection_Examples;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples.TCP_Socket_Connection_Examples
{
    public class TcpSocketClientExample : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "TCP Socket Client Example";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            StreamSocketClientOptions clientOptions = new StreamSocketClientOptions(2);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            using StreamSocketClient client = new StreamSocketClient(ref rawSocket, clientOptions);

            Encoding dataEncoding = UdpSocketServerExample.ServerEncoding;
            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = TcpSocketServerExample.ServerEndPoint;

            client.Connect(in remoteEndPoint);

            /* a cancellable asynchronous version also exists.
            client.ConnectAsync(in remoteEndPoint, CancellationToken.None);
            */

            Console.WriteLine("Starting TCP Socket Client!");

            for (int i = 0; i < 10; i++)
            {
                string data = $"Hello World from {client.LocalEndPoint}!";
                dataEncoding.GetBytes(data).CopyTo(sendBuffer, 0);

                TransmissionResult sendResult =
                    client.Send(in remoteEndPoint, sendBuffer, SocketFlags.None);

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult sendResult =
                    await client.SendAsync(sendBuffer, SocketFlags.None, CancellationToken.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Sent request with contents \'{data}\' to {remoteEndPoint}");
                }

                TransmissionResult receiveResult =
                    client.Receive(in remoteEndPoint, receiveBuffer, SocketFlags.None);

                /* a cancellable asynchronous version also exists. use only when necessary due to the inherent performance penalty of async operations
                TransmissionResult receiveResult =
                    await client.ReceiveAsync(receiveBuffer, SocketFlags.None, CancellationToken.None);
                */

                // lock is not necessary, but means that console output is clean and not interleaved
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Client] Received response with contents \'{dataEncoding.GetString(receiveBuffer).TrimEnd('\0', ' ')}\' from {remoteEndPoint}");
                }
            }

            rawSocket.Shutdown(SocketShutdown.Both);
            rawSocket.Close();
            rawSocket.Dispose();
        }
    }
}