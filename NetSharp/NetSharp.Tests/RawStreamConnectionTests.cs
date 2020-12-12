using System;
using System.Net;
using System.Net.Sockets;

using JetBrains.dotMemoryUnit;

using NetSharp.Raw;
using NetSharp.Raw.Stream;

using Xunit;

namespace NetSharp.Tests
{
    public class RawStreamConnectionTests
    {
        private static readonly IPEndPoint ClientLocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        private static readonly IPEndPoint ServerLocalEndPoint = new IPEndPoint(IPAddress.Loopback, 12345);

        [Fact]
        public void DisposesCleanly()
        {
            static void Instantiate()
            {
                using RawStreamConnection conn = ConnectionFactory();
                conn.Bind(ServerLocalEndPoint);

                conn.Start();

                conn.Close();
            }

            Instantiate();

            AssertDisposedCleanly();
        }

        [Fact]
        public void FullTest()
        {
            using RawStreamConnection server = ConnectionFactory();
            server.Bind(ServerLocalEndPoint);

            static void PacketHandler(
                EndPoint point,
                in RawPacketHeader header,
                in ReadOnlyMemory<byte> data,
                IRawStreamWriter writer)
            {
            }

            server.RegisterHandler(0, PacketHandler);
            server.Start();

            using RawStreamConnection client = ConnectionFactory();
            client.Bind(ClientLocalEndPoint);

            client.ConnectAsync(ServerLocalEndPoint).GetAwaiter().GetResult();

            byte[] packet = { 42 };
            _ = client.SendAsync(0, packet).GetAwaiter().GetResult();

            client.DisconnectAsync().GetAwaiter().GetResult();
            client.Close();

            server.DeregisterHandler(0, PacketHandler);
            server.Close();

            AssertDisposedCleanly();
        }

        private static void AssertDisposedCleanly()
        {
            _ = dotMemory.Check(memory =>
            {
                Assert.Equal(0, memory.GetObjects(where => where.Type.Is<RawStreamConnection>()).ObjectsCount);
            });
        }

        private static RawStreamConnection ConnectionFactory()
        {
            return new RawStreamConnection(ProtocolType.Tcp, new IPEndPoint(IPAddress.Loopback, 0));
        }
    }
}
