namespace NetSharp.Utils.Socket_Options
{
    /// <summary>
    /// Enumerates the possible socket option manager types to instantiate for a <see cref="Client"/> and <see cref="Server"/>
    /// instance.
    /// </summary>
    public enum SocketOptionManager
    {
        /// <summary>
        /// Causes a <see cref="DefaultSocketOptions"/> instance to be created as the socket option manager.
        /// This means that certain socket options will throw an error, as the socket type is not specified.
        /// </summary>
        Default,

        /// <summary>
        /// Causes a <see cref="TcpSocketOptions"/> instance to be created as the socket option manager.
        /// </summary>
        Tcp,

        /// <summary>
        /// Causes a <see cref="UdpSocketOptions"/> instance to be created as the socket option manager.
        /// </summary>
        Udp,
    }
}