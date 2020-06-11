using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using NetSharp.Raw.Stream;

namespace NetSharpExamples.Examples.Stream_Network_Connection_Examples
{
    internal class StreamNetworkWriterAsyncExample : INetSharpExample
    {
        private const int PacketSize = 8192;

        public static readonly EndPoint ClientEndPoint = Program.DefaultClientEndPoint;
        public static readonly Encoding ServerEncoding = Program.DefaultEncoding;
        public static readonly EndPoint ServerEndPoint = Program.DefaultServerEndPoint;

        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Network Writer Example (Asynchronous)";

        /// <inheritdoc />
        public async Task RunAsync()
        {
            EndPoint defaultEndPoint = new IPEndPoint(IPAddress.Any, 0);

            Socket rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            rawSocket.Bind(ClientEndPoint);
            rawSocket.Connect(ServerEndPoint);

            using RawStreamNetworkWriter writer = new RawStreamNetworkWriter(ref rawSocket, defaultEndPoint, PacketSize);

            byte[] transmissionBuffer = new byte[PacketSize];

            EndPoint remoteEndPoint = ServerEndPoint;

            try
            {
                while (true)
                {
                    int sent = await writer.WriteAsync(remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Sent {sent} bytes to {remoteEndPoint}!");
                    }

                    int received = await writer.ReadAsync(remoteEndPoint, transmissionBuffer);

                    lock (typeof(Console))
                    {
                        Console.WriteLine($"Received {received} bytes from {remoteEndPoint}!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                rawSocket.Shutdown(SocketShutdown.Both);
                rawSocket.Disconnect(true);

                rawSocket.Close();
                rawSocket.Dispose();
            }
        }
    }
}