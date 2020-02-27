using System.Net;
using System.Threading.Tasks;
using NetSharp.Utils;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Provides additional methods and functionality to the <see cref="Server"/> class.
    /// </summary>
    public static class ServerExtensions
    {
        /// <summary>
        /// Starts the server synchronously and starts accepting client connections. Blocks.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        public static void Run(this Server instance, IPAddress localAddress, int localPort) =>
            instance.RunAsync(localAddress, localPort).Wait();

        /// <summary>
        /// Starts the server synchronously and starts accepting client connections. Blocks. Uses the default connection port.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to.</param>
        public static void Run(this Server instance, IPAddress localAddress) =>
            instance.RunAsync(localAddress, Constants.DefaultPort).Wait();

        /// <summary>
        /// Starts the server asynchronously and starts accepting client connections. Does not block. Uses the default
        /// connection port.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to.</param>
        public static async Task RunAsync(this Server instance, IPAddress localAddress) =>
            await instance.RunAsync(localAddress, Constants.DefaultPort);

        /// <summary>
        /// Starts the server asynchronously and starts accepting client connections. Does not block.
        /// </summary>
        /// <param name="instance">The instance on which this extension method should be called.</param>
        /// <param name="localAddress">The local IP address to bind to.</param>
        /// <param name="localPort">The local port to bind to.</param>
        public static async Task RunAsync(this Server instance, IPAddress localAddress, int localPort) =>
            await instance.RunAsync(new IPEndPoint(localAddress, localPort));
    }
}