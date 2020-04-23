using NetSharpExamples.Benchmarks;

using System;
using System.Threading.Tasks;
using NetSharpExamples.Examples;

namespace NetSharpExamples
{
    internal class Program
    {
        private static readonly INetSharpExample[] Examples =
        {
            // UDP socket server and client examples
            new UdpSocketServerBenchmark(),
            new UdpSocketServerExample(),
            new UdpSocketClientSyncBenchmark(),
            new UdpSocketClientAsyncBenchmark(),
            new UdpSocketClientExample(),

            // TCP socket server and client examples
            new TcpSocketServerBenchmark(),
            new TcpSocketServerExample(),
            new TcpSocketClientSyncBenchmark(),
            new TcpSocketClientAsyncBenchmark(),
            new TcpSocketClientExample(),
        };

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            while (true)
            {
                PickExample();
            }
        }

        private static void PickExample()
        {
            Console.WriteLine("Available Examples:");
            for (int i = 0; i < Examples.Length; i++)
            {
                Console.WriteLine($"\t{i} - {Examples[i].Name}");
            }

            while (true)
            {
                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine();
                    int choice = int.Parse(rawInput ?? "x");

                    if (0 > choice || choice >= Examples.Length)
                    {
                        Console.WriteLine("Option does not exist. Please try again!");
                        continue;
                    }

                    Examples[choice].RunAsync().GetAwaiter().GetResult();

                    break;
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid option selected. Please try again!");
                }
            }

            Console.WriteLine();
        }
    }
}