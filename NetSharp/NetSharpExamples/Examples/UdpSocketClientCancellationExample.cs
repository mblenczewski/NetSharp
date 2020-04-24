using NetSharp.Packets;
using NetSharp.Sockets.Datagram;
using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharpExamples.Examples
{
    public class UdpSocketClientCancellationExample : INetSharpExample
    {
        private static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12377);

        /// <inheritdoc />
        public string Name { get; } = "UDP Socket Client Cancellation Example";

        /// <summary>
        /// A read only server. Never sends out data!
        /// </summary>
        private Task ServerTask(CancellationToken cancellationToken)
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            server.Bind(ServerEndPoint);

            byte[] transmissionBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                server.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);
            }

            server.Close();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            using CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = Task.Factory.StartNew(state => ServerTask((CancellationToken)state), serverCts.Token, TaskCreationOptions.LongRunning);

            DatagramSocketClientOptions clientOptions = new DatagramSocketClientOptions((ushort)2);
            using DatagramSocketClient client = new DatagramSocketClient(AddressFamily.InterNetwork, ProtocolType.Udp, clientOptions);

            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            TimeSpan timeout = TimeSpan.FromMilliseconds(500);

            Console.WriteLine("Starting UDP Socket Client!");

            for (int i = 0; i < 10; i++)
            {
                CancellationTokenSource sendCts = new CancellationTokenSource();

                sendCts.CancelAfter(timeout);
                TransmissionResult sendResult = await client.SendToAsync(remoteEndPoint, sendBuffer, SocketFlags.None, sendCts.Token);

                if (sendResult.TimedOut())
                {
                    Console.WriteLine("Send timed out!");

                    continue;
                }

                Console.WriteLine($"Sent {sendResult.Count} bytes of data!");

                CancellationTokenSource receiveCts = new CancellationTokenSource();

                receiveCts.CancelAfter(timeout);
                TransmissionResult receiveResult = await client.ReceiveFromAsync(remoteEndPoint, receiveBuffer, SocketFlags.None, receiveCts.Token);

                if (receiveResult.TimedOut())
                {
                    Console.WriteLine("Receive timed out!");

                    continue;
                }

                Console.WriteLine($"Received {receiveResult.Count} bytes of data!");
            }
        }
    }
}