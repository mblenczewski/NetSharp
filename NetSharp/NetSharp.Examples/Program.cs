using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace NetSharp.Examples
{
    public static class Constants
    {
        public const int PacketSize = 8192, PacketCount = 1_000_000, ClientCount = 10;

        public static readonly EndPoint ClientEndPoint = new IPEndPoint(IPAddress.Any, 0);
        public static readonly EndPoint DefaultEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        public static readonly Encoding ServerEncoding = Encoding.UTF8;
        public static readonly EndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 12345);
    }

    internal class Program
    {
        private static readonly List<INetSharpExample> Examples = new List<INetSharpExample>();

        private static void Main()
        {
            while (true)
            {
                ResetBenchmarks();

                GC.Collect();

                PickBenchmark();
            }
        }

        private static void PickBenchmark()
        {
            const string allBenchmarkIdentifier = "XX";

            while (true)
            {
                Console.WriteLine("Available Examples:");
                for (int i = 0; i < Examples.Count; i++)
                {
                    Console.WriteLine($"\t{i:D2} - {Examples[i].Name}");
                }

                Console.Write("> ");

                try
                {
                    string rawInput = Console.ReadLine()?.ToLowerInvariant() ?? "x";

                    int choice = int.Parse(rawInput);

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

        private static void ResetBenchmarks()
        {
            Examples.Clear();

            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                Type[] interfaces = type.GetInterfaces();
                if (interfaces.Contains(typeof(INetSharpExample)))
                {
                    Examples.Add((INetSharpExample)Activator.CreateInstance(type));
                }
            }
        }
    }
}
