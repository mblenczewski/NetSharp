using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NetSharp;
using NetSharp.Packets;

namespace NetSharpExamples.Examples.Connection_Examples
{
    public class DefaultConnectionExample : INetSharpExample
    {
        /// <inheritdoc />
        public string Name { get; } = "Connection Instantiation Code Example";

        /// <inheritdoc />
        public Task RunAsync()
        {
            using Connection streamConnection = ConnectionBuilder
                .WithStreamTransport()
                .WithInterNetwork(new IPEndPoint(IPAddress.Any, 0))
                .WithSettings(NetworkPacket.TotalSize, 1000, 0)
                .BuildDefault();

            using Connection datagramConnection = ConnectionBuilder
                .WithDatagramTransport()
                .WithDefaultSettings(1024)
                .BuildDefault();

            return Task.CompletedTask;
        }
    }
}