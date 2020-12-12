using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetSharp.Raw
{
    /// <summary>
    /// Contains metadata about a network packet.
    /// </summary>
    public readonly struct RawPacketHeader : IEquatable<RawPacketHeader>
    {
        /// <summary>
        /// The total length of the packet when serialised.
        /// </summary>
        public const int Length = sizeof(ushort) + sizeof(int);

#pragma warning disable CA1051

        /// <summary>
        /// The length of the data following this packet.
        /// </summary>
        public readonly int DataLength;

        /// <summary>
        /// The packet type.
        /// </summary>
        public readonly ushort Type;

#pragma warning restore CA1051

        internal RawPacketHeader(ushort type, int dataLength)
        {
            Type = type;
            DataLength = dataLength;
        }

        /// <summary>
        /// Checks whether two <see cref="RawPacketHeader" /> instances are not equal.
        /// </summary>
        /// <param name="left">
        /// The first instance.
        /// </param>
        /// <param name="right">
        /// The second instance.
        /// </param>
        /// <returns>
        /// Whether the two instances are not equal.
        /// </returns>
        public static bool operator !=(RawPacketHeader left, RawPacketHeader right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Checks whether two <see cref="RawPacketHeader" /> instances are equal.
        /// </summary>
        /// <param name="left">
        /// The first instance.
        /// </param>
        /// <param name="right">
        /// The second instance.
        /// </param>
        /// <returns>
        /// Whether the two instances are equal.
        /// </returns>
        public static bool operator ==(RawPacketHeader left, RawPacketHeader right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is RawPacketHeader other && Equals(other);
        }

        /// <inheritdoc />
        public bool Equals(RawPacketHeader other)
        {
            return DataLength == other.DataLength && Type == other.Type;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(DataLength, Type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static RawPacketHeader Deserialise(in ReadOnlySpan<byte> buffer)
        {
            int offset = 0;

            ushort type = MemoryMarshal.Read<ushort>(buffer.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);

            int dataSize = MemoryMarshal.Read<int>(buffer.Slice(offset, sizeof(int)));

            return new RawPacketHeader(type, dataSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Serialise(in Span<byte> buffer)
        {
            int offset = 0;

            ushort type = Type;
            MemoryMarshal.Write(buffer.Slice(offset, sizeof(ushort)), ref type);
            offset += sizeof(ushort);

            int dataSize = DataLength;
            MemoryMarshal.Write(buffer.Slice(offset, sizeof(int)), ref dataSize);
        }
    }
}
