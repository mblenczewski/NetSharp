using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace NetSharpExamples
{
    internal class Program
    {
        private static readonly List<INetSharpBenchmark> Benchmarks;
        private static readonly List<INetSharpExample> Examples;

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
                        Examples.Add((INetSharpExample)instance);
                        break;

                    case 2 when type.GetInterface(nameof(INetSharpBenchmark)) == typeof(INetSharpBenchmark):
                        Examples.Add((INetSharpExample)instance);
                        Benchmarks.Add((INetSharpBenchmark)instance);
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
                Console.WriteLine($"\t{i} - {Examples[i].Name}");
            }

            while (true)
            {
                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine();
                    int choice = int.Parse(rawInput ?? "x");

                    if (0 > choice || choice >= Examples.Count)
                    {
                        Console.WriteLine("Option does not exist. Please try again!");
                        continue;
                    }

                    Console.WriteLine($"Starting \'{Examples[choice].Name}\'...");
                    Examples[choice].RunAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"Finished \'{Examples[choice].Name}\'!");

                    break;
                }
                catch (FormatException)
                {
                    Console.WriteLine("Invalid option selected. Please try again!");
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