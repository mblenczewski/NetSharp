using NetSharpExamples.Benchmarks;

using System;
using System.Threading.Tasks;

namespace NetSharpExamples
{
    internal class Program
    {
        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            INetSharpExample udpSocketServerBenchmark = new UdpSocketServerBenchmark();
            await udpSocketServerBenchmark.RunAsync();

            INetSharpExample tcpSocketServerBenchmark = new TcpSocketServerBenchmark();
            await tcpSocketServerBenchmark.RunAsync();

            Console.ReadLine();
        }
    }
}