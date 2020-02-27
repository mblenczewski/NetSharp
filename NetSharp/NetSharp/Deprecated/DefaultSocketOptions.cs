using System;
using System.Net.Sockets;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Allows for manipulation of socket options.
    /// </summary>
    public sealed class DefaultSocketOptions : SocketOptions
    {
        /// <inheritdoc />
        public DefaultSocketOptions(ref Socket socket) : base(ref socket)
        {
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">
        /// This property is not supported when using the default socket option manager.
        /// </exception>
        public override int HopLimit
        {
            get { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
            set { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">
        /// This property is not supported when using the default socket option manager.
        /// </exception>
        public override bool IsRoutingEnabled
        {
            get { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
            set { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
        }

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">
        /// This property is not supported when using the default socket option manager.
        /// </exception>
        public override bool UseLoopback
        {
            get { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
            set { throw new NotSupportedException("This property is not supported in the default socket options manager."); }
        }
    }
}