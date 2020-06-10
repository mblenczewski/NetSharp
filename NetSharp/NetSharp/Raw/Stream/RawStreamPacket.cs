using System;
using System.Runtime.CompilerServices;

using NetSharp.Utils.Conversion;

namespace NetSharp.Raw.Stream
{
    public readonly struct RawStreamPacket
    {
        public static (RawStreamPacketHeader packetHeader, ReadOnlyMemory<byte> packetData) Deserialise(in Memory<byte> buffer)
        {
            Memory<byte> serialisedHeader = buffer.Slice(0, RawStreamPacketHeader.TotalSize);
            RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in serialisedHeader);

            return (header, buffer.Slice(RawStreamPacketHeader.TotalSize));
        }

        public static void Serialise(in Memory<byte> buffer, in RawStreamPacketHeader packetHeader, in ReadOnlyMemory<byte> packetData)
        {
            packetHeader.Serialise(buffer.Slice(0, RawStreamPacketHeader.TotalSize));

            packetData.CopyTo(buffer.Slice(RawStreamPacketHeader.TotalSize, packetData.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TotalPacketSize(in RawStreamPacketHeader packetHeader)
        {
            return RawStreamPacketHeader.TotalSize + packetHeader.DataSize;
        }
    }

    public readonly struct RawStreamPacketHeader
    {
        public const int TotalSize = sizeof(int);

        public readonly int DataSize;

        internal RawStreamPacketHeader(int dataSize)
        {
            DataSize = dataSize;
        }

        public static RawStreamPacketHeader Deserialise(in Memory<byte> buffer)
        {
            Span<byte> serialisedDataSize = buffer.Slice(0, sizeof(int)).Span;
            int dataSize = EndianAwareBitConverter.ToInt32(serialisedDataSize);

            return new RawStreamPacketHeader(dataSize);
        }

        public void Serialise(in Memory<byte> buffer)
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