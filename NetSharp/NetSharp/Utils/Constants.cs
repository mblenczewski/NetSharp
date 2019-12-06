namespace NetSharp.Utils
{
    /// <summary>
    /// Holds internal default configurations and constants.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// The default port over which a connection is made.
        /// </summary>
        internal const int DefaultPort = 12374;

        /// <summary>
        /// The largest byte buffer that can be sent via UDP.
        /// </summary>
        internal const int UdpMaxBufferSize = 60_000;
    }
}