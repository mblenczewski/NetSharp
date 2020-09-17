using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetSharp.Raw.Stream
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
        /// Initialises a new instance of the <see cref="RawStreamPacketHeader"/> struct.
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
            int dataSize = MemoryMarshal.Read<int>(buffer.Span.Slice(0, sizeof(int)));

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
            int dataSize = DataSize;

            MemoryMarshal.Write(buffer.Span.Slice(0, sizeof(int)), ref dataSize);
        }
    }
}
