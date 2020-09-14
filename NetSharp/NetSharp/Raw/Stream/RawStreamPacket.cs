using System;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Provides helper methods to manipulate the binary packet format used by stream network handlers.
    /// </summary>
    internal static class RawStreamPacket
    {
        /// <summary>
        /// Serialises the given <paramref name="packetHeader" /> and <paramref name="packetData" /> into the given <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">
        /// The buffer into which the packet should be serialised. Must be at least of size <see cref="RawStreamPacketHeader.TotalSize" /> + the size
        /// of the user data given by <paramref name="packetHeader" />.
        /// </param>
        /// <param name="packetHeader">
        /// The header containing metatdata abut the raw stream packet.
        /// </param>
        /// <param name="packetData">
        /// The user data held in the raw stream packet.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialise(in Memory<byte> buffer, in RawStreamPacketHeader packetHeader, in ReadOnlyMemory<byte> packetData)
        {
            packetHeader.Serialise(buffer.Slice(0, RawStreamPacketHeader.TotalSize));

            packetData.CopyTo(buffer.Slice(RawStreamPacketHeader.TotalSize, packetData.Length));
        }

        /// <summary>
        /// Calculates the total size of a raw stream packet, using the packet data size in the given <paramref name="packetHeader" />.
        /// </summary>
        /// <param name="packetHeader">
        /// The header for which to calculate the total packet size.
        /// </param>
        /// <returns>
        /// The total size of a raw stream packet with the given <paramref name="packetHeader" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TotalPacketSize(in RawStreamPacketHeader packetHeader)
        {
            return RawStreamPacketHeader.TotalSize + packetHeader.DataSize;
        }
    }
}
