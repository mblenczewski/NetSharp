using System;

using NetSharp.Utils.Conversion;

namespace NetSharp.Raw.Stream
{
    public readonly struct RawStreamPacket
    {
        public readonly Memory<byte> Data;
        public readonly Header PacketHeader;

        private RawStreamPacket(in Header header, in Memory<byte> data)
        {
            PacketHeader = header;

            Data = data;
        }

        public RawStreamPacket(in Memory<byte> data)
        {
            PacketHeader = new Header(data.Length);

            Data = data;
        }

        public static RawStreamPacket Deserialise(in Memory<byte> buffer)
        {
            Memory<byte> serialisedHeader = buffer.Slice(0, Header.TotalHeaderSize);
            Header header = Header.Deserialise(in serialisedHeader);

            Memory<byte> serialisedData = buffer.Slice(Header.TotalHeaderSize);

            return new RawStreamPacket(in header, in serialisedData);
        }

        public void Serialise(in Memory<byte> buffer)
        {
            PacketHeader.Serialise(buffer.Slice(0, Header.TotalHeaderSize));

            Data.CopyTo(buffer.Slice(Header.TotalHeaderSize, Data.Length));
        }

        public readonly struct Header
        {
            public const int TotalHeaderSize = sizeof(int);

            public readonly int DataSize;

            internal Header(int dataSize)
            {
                DataSize = dataSize;
            }

            public static Header Deserialise(in Memory<byte> buffer)
            {
                Span<byte> serialisedDataSize = buffer.Slice(0, sizeof(int)).Span;
                int dataSize = EndianAwareBitConverter.ToInt32(serialisedDataSize);

                return new Header(dataSize);
            }

            public void Serialise(in Memory<byte> buffer)
            {
                Span<byte> serialisedDataSize = EndianAwareBitConverter.GetBytes(DataSize);
                serialisedDataSize.CopyTo(buffer.Slice(0, sizeof(int)).Span);
            }
        }
    }
}