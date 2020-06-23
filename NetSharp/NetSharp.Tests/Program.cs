using System;
using System.Net;
using System.Resources;
using System.Text;

[assembly: NeutralResourcesLanguage("en")]

namespace NetSharp.Tests
{
    internal class Program
    {
        private static void Main()
        {
            Console.WriteLine("Hello World!");
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