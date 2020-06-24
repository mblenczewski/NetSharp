using System;
using System.Runtime.CompilerServices;

using NetSharp.Utils.Conversion;

namespace NetSharp.Packets
{
    /// <summary>
    /// Holds metadata about a raw stream packet.
    /// </summary>
    internal readonly struct RawStreamPacketHeader
    {
        /// <summary>
        /// The total size of the header in bytes.
        /// </summary>
        internal const int TotalSize = sizeof(int);

        /// <summary>
        /// The size of the user supplied data segment in bytes.
        /// </summary>
        internal readonly int DataSize;

        /// <summary>
        /// Constructs a new instance of the <see cref="RawStreamPacketHeader" /> struct.
        /// </summary>
        /// <param name="dataSize">
        /// The size of the user supplied data segment.
        /// </param>
        internal RawStreamPacketHeader(int dataSize)
        {
            DataSize = dataSize;
        }

        /// <summary>
        /// Deserialises a <see cref="RawStreamPacketHeader" /> instance from the given <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">
        /// A buffer containing a serialised <see cref="RawStreamPacketHeader" /> instance. Must be at least of size <see cref="TotalSize" />.
        /// </param>
        /// <returns>
        /// The deserialised instance.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RawStreamPacketHeader Deserialise(in Memory<byte> buffer)
        {
            Span<byte> serialisedDataSize = buffer.Slice(0, sizeof(int)).Span;
            int dataSize = EndianAwareBitConverter.ToInt32(serialisedDataSize);

            return new RawStreamPacketHeader(dataSize);
        }

        /// <summary>
        /// Serialises the current <see cref="RawStreamPacketHeader" /> instance into the given <paramref name="buffer" />.
        /// </summary>
        /// <param name="buffer">
        /// The buffer into which to serialise the current instance. Must be at least of size <see cref="TotalSize" />.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Serialise(in Memory<byte> buffer)
        {
            Span<byte> serialisedDataSize = EndianAwareBitConverter.GetBytes(DataSize);
            serialisedDataSize.CopyTo(buffer.Slice(0, sizeof(int)).Span);
        }
    }

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