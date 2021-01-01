using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Describes the interface for a stream network connection that can write to the network.
    /// </summary>
    public interface IRawStreamWriter
    {
        /// <summary>
        /// Writes the given packet header and data to the network asynchronously, using the given socket flags for the transmission.
        /// </summary>
        /// <param name="type">
        /// The type of packet being written to the network.
        /// </param>
        /// <param name="buffer">
        /// The data held by the packet being written to the network.
        /// </param>
        /// <param name="flags">
        /// The <see cref="SocketFlags" /> to use for the transmission.
        /// </param>
        /// <returns>
        /// The number of bytes written to the network.
        /// </returns>
        ValueTask<int> SendAsync(ushort type, ReadOnlyMemory<byte> buffer, SocketFlags flags = SocketFlags.None);
    }
}
