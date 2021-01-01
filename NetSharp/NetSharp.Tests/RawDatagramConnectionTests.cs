using System.Net;
using System.Net.Sockets;

using JetBrains.dotMemoryUnit;

using NetSharp.Raw.Datagram;

using Xunit;

namespace NetSharp.Tests
{
    public class RawDatagramConnectionTests
    {
        private static readonly IPEndPoint ClientLocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        private static readonly IPEndPoint ServerLocalEndPoint = new IPEndPoint(IPAddress.Loopback, 12345);

        [Fact]
        public void DisposesCleanly()
        {
            static void Instantiate()
            {
                using RawDatagramConnection conn = ConnectionFactory();

                conn.Bind(ClientLocalEndPoint);
                conn.Start();

                conn.Close();
            }

            Instantiate();

            AssertDisposedCleanly();
        }

        private static void AssertDisposedCleanly()
        {
            _ = dotMemory.Check(memory =>
            {
                Assert.Equal(0, memory.GetObjects(where => where.Type.Is<RawDatagramConnection>()).ObjectsCount);
            });
        }

        private static RawDatagramConnection ConnectionFactory()
        {
            return new RawDatagramConnection(ProtocolType.Udp, new IPEndPoint(IPAddress.Loopback, 0));
        }
    }
}
