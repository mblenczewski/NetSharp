using System;
using System.Runtime.CompilerServices;

using NetSharp.Utils.Conversion;

namespace NetSharp.Raw.Stream
{
    public readonly struct RawStreamPacket
    {
        public readonly Memory<byte> Data;
        public readonly RawStreamPacketHeader Header;

        private RawStreamPacket(in RawStreamPacketHeader packetHeader, in Memory<byte> packetData)
        {
            Header = packetHeader;

            Data = packetData;
        }

        public RawStreamPacket(in Memory<byte> packetData)
        {
            Header = new RawStreamPacketHeader(packetData.Length);

            Data = packetData;
        }

        public static RawStreamPacket Deserialise(in Memory<byte> buffer)
        {
            Memory<byte> serialisedHeader = buffer.Slice(0, RawStreamPacketHeader.TotalSize);
            RawStreamPacketHeader header = RawStreamPacketHeader.Deserialise(in serialisedHeader);

            return new RawStreamPacket(in header, in buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TotalPacketSize(int packetDataSize)
        {
            return RawStreamPacketHeader.TotalSize + packetDataSize;
        }

        public void Serialise(in Memory<byte> buffer)
        {
            Header.Serialise(buffer.Slice(0, RawStreamPacketHeader.TotalSize));

            Data.CopyTo(buffer.Slice(RawStreamPacketHeader.TotalSize, Data.Length));
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
    }
}