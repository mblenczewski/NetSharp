using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

[assembly: NeutralResourcesLanguage("en")]

namespace NetSharpExamples
{
    internal class Program
    {
        private static readonly List<INetSharpBenchmark> Benchmarks;
        private static readonly IPAddress DefaultExampleAddress = IPAddress.Loopback;
        private static readonly int DefaultExamplePort = 44230;
        private static readonly List<INetSharpExample> Examples;

        public static readonly EndPoint DefaultClientEndPoint = new IPEndPoint(DefaultExampleAddress, 0);
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;
        public static readonly EndPoint DefaultServerEndPoint = new IPEndPoint(DefaultExampleAddress, DefaultExamplePort);

        static Program()
        {
            Examples = new List<INetSharpExample>();

            Benchmarks = new List<INetSharpBenchmark>();

            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (!type.IsClass)
                {
                    continue;
                }

                Type[] interfaces = type.GetInterfaces();
                object instance = Activator.CreateInstance(type);

                switch (interfaces.Length)
                {
                    case 1 when type.GetInterface(nameof(INetSharpExample)) == typeof(INetSharpExample):
                        Examples.Add((INetSharpExample) instance);
                        break;

                    case 2 when type.GetInterface(nameof(INetSharpBenchmark)) == typeof(INetSharpBenchmark):
                        Examples.Add((INetSharpExample) instance);
                        Benchmarks.Add((INetSharpBenchmark) instance);
                        break;
                }
            }
        }

        private static async Task Main()
        {
            Console.WriteLine("Hello World!");

            await RunAllBenchmarks();

            while (true)
            {
                GC.Collect();

                PickExample();
            }
        }

        private static void PickExample()
        {
            Console.WriteLine("Available Examples:");
            for (int i = 0; i < Examples.Count; i++)
            {
                Console.WriteLine($"\t{i:D2} - {Examples[i].Name}");
            }

            while (true)
            {
                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine();
                    int choice = int.Parse(rawInput ?? "x");

                    if (choice < 0 || choice >= Examples.Count)
                    {
                        Console.WriteLine("Option does not exist. Please try again!");
                        continue;
                    }

                    INetSharpExample selectedExample = Examples[choice];

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

        private static async Task RunAllBenchmarks()
        {
            Console.WriteLine("Run all benchmarks? (y/n)");
            string input = Console.ReadLine()?.ToLowerInvariant() ?? "n";

            if (input == "y")
            {
                foreach (INetSharpBenchmark benchmark in Benchmarks)
                {
                    Console.WriteLine($"Starting \'{benchmark.Name}\'...");
                    await benchmark.RunAsync();
                    Console.WriteLine($"Finished \'{benchmark.Name}\'!");

                    Console.WriteLine();
                }
            }
        }
    }
}