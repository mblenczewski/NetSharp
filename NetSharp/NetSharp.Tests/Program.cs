using System;
using System.Net;
using System.Resources;
using System.Text;

[assembly: NeutralResourcesLanguage("en")]

namespace NetSharp.Tests
{
    internal class Program
    {
        private const int DefaultExamplePort = 44232;
        private static readonly IPAddress DefaultExampleAddress = IPAddress.Loopback;
        public static readonly EndPoint DefaultClientEndPoint = new IPEndPoint(DefaultExampleAddress, 0);
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;
        public static readonly EndPoint DefaultServerEndPoint = new IPEndPoint(DefaultExampleAddress, DefaultExamplePort);

        private static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}