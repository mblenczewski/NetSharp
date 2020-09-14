using System.Net;
using System.Text;

using Xunit;

namespace NetSharp.Tests
{
    public class Program
    {
        [Fact]
        public void HelloTests()
        {
            Assert.True(true);
        }

        public static class Constants
        {
            private const int DefaultExamplePort = 44233;
            private static readonly IPAddress DefaultExampleAddress = IPAddress.Loopback;

            public static readonly EndPoint ClientEndPoint = new IPEndPoint(DefaultExampleAddress, 0);
            public static readonly Encoding ServerEncoding = Encoding.UTF8;
            public static readonly EndPoint ServerEndPoint = new IPEndPoint(DefaultExampleAddress, DefaultExamplePort);
        }
    }
}