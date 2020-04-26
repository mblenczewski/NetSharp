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
        private Task ServerTask()
        {
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            server.Bind(ServerEndPoint);

            byte[] transmissionBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            for (int i = 0; i < 10; i++)
            {
                server.ReceiveFrom(transmissionBuffer, ref remoteEndPoint);
            }

            for (int i = 0; i < 9; i++)
            {
                server.SendTo(transmissionBuffer, remoteEndPoint);
            }

            server.Close();
            server.Dispose();

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task RunAsync()
        {
            Task serverTask = Task.Factory.StartNew(ServerTask, TaskCreationOptions.LongRunning);

            DatagramSocketClientOptions clientOptions = new DatagramSocketClientOptions((ushort)2);
            DatagramSocketClient client = new DatagramSocketClient(AddressFamily.InterNetwork, ProtocolType.Udp, clientOptions);

            byte[] sendBuffer = new byte[NetworkPacket.TotalSize];
            byte[] receiveBuffer = new byte[NetworkPacket.TotalSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            TimeSpan timeout = TimeSpan.FromMilliseconds(500);

            Console.WriteLine("Starting UDP Socket Client!");

            for (int i = 0; i < 10; i++)
            {
                using CancellationTokenSource sendCts = new CancellationTokenSource();
                using CancellationTokenSource receiveCts = new CancellationTokenSource();

                sendCts.CancelAfter(timeout);
                TransmissionResult sendResult = await client.SendAsync(remoteEndPoint, sendBuffer, SocketFlags.None, sendCts.Token);

                if (sendResult.TimedOut())
                {
                    Console.WriteLine("Send timed out!");
                }
                else
                {
                    Console.WriteLine($"Sent {sendResult.Count} bytes of data!");

                    receiveCts.CancelAfter(timeout);
                    TransmissionResult receiveResult = await client.ReceiveAsync(remoteEndPoint, receiveBuffer, SocketFlags.None, receiveCts.Token);

                    if (receiveResult.TimedOut())
                    {
                        Console.WriteLine("Receive timed out!");
                    }
                    else
                    {
                        Console.WriteLine($"Received {receiveResult.Count} bytes of data!");
                    }
                }
            }

            client.Dispose();

            Console.WriteLine($"UDP Socket Client finished!");
        }
    }
}