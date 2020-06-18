using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;

[assembly: NeutralResourcesLanguage("en")]

namespace NetSharp.Examples
{
    internal class Program
    {
        private const int DefaultExamplePort = 44230;
        private static readonly IPAddress DefaultExampleAddress = IPAddress.Loopback;
        private static readonly List<INetSharpExample> Examples;
        public static readonly EndPoint DefaultClientEndPoint = new IPEndPoint(DefaultExampleAddress, 0);
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;
        public static readonly EndPoint DefaultServerEndPoint = new IPEndPoint(DefaultExampleAddress, DefaultExamplePort);

        static Program()
        {
            Examples = new List<INetSharpExample>();

            foreach (Type type in Assembly.GetCallingAssembly().GetTypes())
            {
                if (!type.IsClass && !type.IsValueType)
                {
                    continue;
                }

                Type[] interfaces = type.GetInterfaces();
                object instance = Activator.CreateInstance(type);

                if (type.GetInterface(nameof(INetSharpExample)) == typeof(INetSharpExample))
                {
                    Examples.Add((INetSharpExample) instance);
                }
            }
        }

        private static void Main()
        {
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
    }
}