using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;

[assembly: NeutralResourcesLanguage("en")]

namespace NetSharp.Benchmarks
{
    internal class Program
    {
        private static readonly List<INetSharpBenchmark> Benchmarks;

        static Program()
        {
            Benchmarks = new List<INetSharpBenchmark>();

            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (!type.IsClass && !type.IsValueType)
                {
                    continue;
                }

                Type[] interfaces = type.GetInterfaces();
                object instance = Activator.CreateInstance(type);

                if (type.GetInterface(nameof(INetSharpBenchmark)) == typeof(INetSharpBenchmark))
                {
                    Benchmarks.Add((INetSharpBenchmark) instance);
                }
            }
        }

        private static void Main()
        {
            RunAllBenchmarks();

            while (true)
            {
                GC.Collect();

                PickBenchmark();
            }
        }

        private static void PickBenchmark()
        {
            Console.WriteLine("Available Examples:");
            for (int i = 0; i < Benchmarks.Count; i++)
            {
                Console.WriteLine($"\t{i:D2} - {Benchmarks[i].Name}");
            }

            while (true)
            {
                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine();
                    int choice = int.Parse(rawInput ?? "x");

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
            Console.WriteLine("Run all benchmarks? (y/n)");
            Console.Write("> ");
            string input = Console.ReadLine()?.ToLowerInvariant() ?? "n";

            if (input == "y")
            {
                foreach (INetSharpBenchmark benchmark in Benchmarks)
                {
                    Console.WriteLine($"Starting \'{benchmark.Name}\'...");
                    benchmark.RunAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"Finished \'{benchmark.Name}\'!");

                    Console.WriteLine();
                }
            }
        }

        public static class Constants
        {
            private const int DefaultExamplePort = 44231;
            private static readonly IPAddress DefaultExampleAddress = IPAddress.Loopback;

            public static readonly EndPoint ClientEndPoint = new IPEndPoint(DefaultExampleAddress, 0);
            public static readonly Encoding ServerEncoding = Encoding.UTF8;
            public static readonly EndPoint ServerEndPoint = new IPEndPoint(DefaultExampleAddress, DefaultExamplePort);
        }
    }
}