using System;
using NetSharp.Utils.Conversion;

namespace NetSharp.Raw.Stream
{
    public readonly struct RawStreamPacket
    {
    }

    public readonly struct RawStreamPacketHeader
    {
        public const int HeaderSize = sizeof(int);
        public readonly int PacketSize;

        private RawStreamPacketHeader(int packetSize)
        {
            PacketSize = packetSize;
        }

        public static RawStreamPacketHeader Deserialise(Memory<byte> buffer)
        {
            Span<byte> packetSizeSpan = buffer.Slice(0, sizeof(int)).Span;

            int packetSize = EndianAwareBitConverter.ToInt32(packetSizeSpan);

            return new RawStreamPacketHeader(packetSize);
        }

        public static void Serialise(RawStreamPacketHeader instance, Memory<byte> buffer)
        {
            Span<byte> packetSizeSpan = buffer.Slice(0, sizeof(int)).Span;
            EndianAwareBitConverter.GetBytes(instance.PacketSize).CopyTo(packetSizeSpan);
        }
    }
}