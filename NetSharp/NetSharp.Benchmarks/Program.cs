using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace NetSharp.Benchmarks
{
    internal class Program
    {
        private static readonly List<INetSharpBenchmark> Benchmarks = new List<INetSharpBenchmark>();

        private static void Main()
        {
            while (true)
            {
                ResetBenchmarks();

                GC.Collect();

                PickBenchmark();
            }
        }

        private static void ResetBenchmarks()
        {
            Benchmarks.Clear();

            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                Type[] interfaces = type.GetInterfaces();
                if (interfaces.Contains(typeof(INetSharpBenchmark)))
                {
                    Benchmarks.Add((INetSharpBenchmark)Activator.CreateInstance(type));
                }
            }
        }

        private static void PickBenchmark()
        {
            const string allBenchmarkIdentifier = "XX";

            while (true)
            {
                Console.WriteLine("Available Examples:");
                for (int i = 0; i < Benchmarks.Count; i++)
                {
                    Console.WriteLine($"\t{i:D2} - {Benchmarks[i].Name}");
                }
                Console.WriteLine($"\t{allBenchmarkIdentifier} - Run All Benchmarks");

                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine()?.ToLowerInvariant() ?? "x";

                    if (rawInput.Equals(allBenchmarkIdentifier.ToLowerInvariant()))
                    {
                        RunAllBenchmarks();

                        continue;
                    }

                    int choice = int.Parse(rawInput);

                    if (choice < 0 || choice >= Benchmarks.Count)
                    {
                        Console.WriteLine("Option does not exist. Please try again!");
                        continue;
                    }

                    INetSharpBenchmark selectedExample = Benchmarks[choice];

                    Console.WriteLine($"Starting \'{selectedExample.Name}\'...");
                    selectedExample.RunAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"Finished \'{selectedExample.Name}\'!");

                    break;
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid option selected. Please try again!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            Console.WriteLine();
        }

        private static void RunAllBenchmarks()
        {
            foreach (INetSharpBenchmark benchmark in Benchmarks)
            {
                Console.WriteLine($"Starting \'{benchmark.Name}\'...");
                benchmark.RunAsync().GetAwaiter().GetResult();
                Console.WriteLine($"Finished \'{benchmark.Name}\'!");

                Console.WriteLine();
            }
        }

        public static class Constants
        {
#if DEBUG
            // Packet counts of over 10_000 can take a long time, depending on the packet size and number of clients
            public const int PacketSize = 4096, PacketCount = 1_000_000, ClientCount = 10;
#else // RELEASE
            public const int PacketSize = 8192, PacketCount = 1_000_000, ClientCount = 10;
#endif
            public static readonly Encoding ServerEncoding = Encoding.UTF8;
            public static readonly EndPoint DefaultEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }
    }
}