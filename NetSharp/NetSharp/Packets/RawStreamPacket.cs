using System;
using System.Runtime.CompilerServices;

using NetSharp.Utils.Conversion;

namespace NetSharp.Packets
{
    internal readonly struct RawStreamPacket
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialise(in Memory<byte> buffer, in RawStreamPacketHeader packetHeader, in ReadOnlyMemory<byte> packetData)
        {
            packetHeader.Serialise(buffer.Slice(0, RawStreamPacketHeader.TotalSize));

            packetData.CopyTo(buffer.Slice(RawStreamPacketHeader.TotalSize, packetData.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TotalPacketSize(in RawStreamPacketHeader packetHeader)
        {
            return RawStreamPacketHeader.TotalSize + packetHeader.DataSize;
        }
    }

    internal readonly struct RawStreamPacketHeader
    {
        internal const int TotalSize = sizeof(int);

        internal readonly int DataSize;

        internal RawStreamPacketHeader(int dataSize)
        {
            DataSize = dataSize;
        }

        internal static RawStreamPacketHeader Deserialise(in Memory<byte> buffer)
        {
            Span<byte> serialisedDataSize = buffer.Slice(0, sizeof(int)).Span;
            int dataSize = EndianAwareBitConverter.ToInt32(serialisedDataSize);

            return new RawStreamPacketHeader(dataSize);
        }

        internal void Serialise(in Memory<byte> buffer)
        {
            Span<byte> serialisedDataSize = EndianAwareBitConverter.GetBytes(DataSize);
            serialisedDataSize.CopyTo(buffer.Slice(0, sizeof(int)).Span);
        }

        public override string ToString()
        {
            return $"[Data Segment Size: {DataSize}]";
        }
    }
}