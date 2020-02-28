using System;
using NetSharp.Utils.Conversion;

namespace NetSharp.Packets
{
    /// <summary>
    /// Represents a low-level packet that is transmitted over the network.
    /// </summary>
    public readonly struct NetworkPacket
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="NetworkPacket"/> struct.
        /// </summary>
        /// <param name="data">The data that should be transmitted in the packet.</param>
        /// <param name="header">The header for the packet.</param>
        /// <param name="footer">The footer for the packet.</param>
        private NetworkPacket(ReadOnlyMemory<byte> data, NetworkPacketHeader header, NetworkPacketFooter footer)
        {
            Header = header;

            DataBuffer = data;

            Footer = footer;
        }

        /// <summary>
        /// The number of bytes allocated in each packet for user data.
        /// </summary>
        public const int DataSegmentSize = PacketSize - HeaderSize - FooterSize;

        /// <summary>
        /// The number of bytes taken up in each packet by its footer.
        /// </summary>
        public const int FooterSize = NetworkPacketFooter.Size;

        /// <summary>
        /// The number of bytes taken up in each packet by its header.
        /// </summary>
        public const int HeaderSize = NetworkPacketHeader.Size;

        /// <summary>
        /// The size of each packet, including its header, footer, and data segment.
        /// </summary>
        public const int PacketSize = 4096;

        /// <summary>
        /// The data held in this packet.
        /// </summary>
        public readonly ReadOnlyMemory<byte> DataBuffer;

        public readonly NetworkPacketFooter Footer;
        public readonly NetworkPacketHeader Header;

        /// <summary>
        /// Initialises a new instance of the <see cref="NetworkPacket"/> struct.
        /// </summary>
        /// <param name="data">The data that should be transmitted in the packet.</param>
        /// <param name="dataLength">The number of bytes that are held in the given data buffer.</param>
        /// <param name="type">The packet type.</param>
        /// <param name="errorCode">The error code associated with this transmission.</param>
        /// <param name="hasSucceedingPacket">Whether this packet has a succeeding packet in the packet chain.</param>
        public NetworkPacket(ReadOnlyMemory<byte> data, int dataLength, uint type, NetworkErrorCode errorCode, bool hasSucceedingPacket)
        {
            Header = new NetworkPacketHeader(type, errorCode, dataLength);

            DataBuffer = data;

            Footer = new NetworkPacketFooter(hasSucceedingPacket);
        }

        /// <summary>
        /// Deserialises the given buffer into a packet instance.
        /// </summary>
        /// <param name="buffer">The byte buffer to serialise.</param>
        /// <returns>The deserialised packet instance.</returns>
        public static NetworkPacket Deserialise(Memory<byte> buffer)
        {
            Span<byte> serialisedPacketHeader = buffer.Slice(0, HeaderSize).Span;
            NetworkPacketHeader header = NetworkPacketHeader.Deserialise(serialisedPacketHeader);

            Span<byte> serialisedPacketFooter = buffer.Slice(HeaderSize + DataSegmentSize, FooterSize).Span;
            NetworkPacketFooter footer = NetworkPacketFooter.Deserialise(serialisedPacketFooter);

            Memory<byte> serialisedInstanceData = buffer.Slice(HeaderSize, DataSegmentSize);

            return new NetworkPacket(serialisedInstanceData, header, footer);
        }

        /// <summary>
        /// Serialises the given packet instance to a new byte buffer.
        /// </summary>
        /// <param name="instance">The packet instance to serialise.</param>
        /// <returns>The byte buffer that represents the packet instance.</returns>
        public static Memory<byte> Serialise(NetworkPacket instance)
        {
            byte[] buffer = new byte[PacketSize];
            SerialiseToBuffer(buffer, instance);
            return buffer;
        }

        /// <summary>
        /// Serialises the given packet instance into the given byte buffer.
        /// </summary>
        /// <param name="buffer">
        /// The buffer to which the instance should be serialised. Must be at least of size <see cref="PacketSize"/>.
        /// </param>
        /// <param name="instance">The packet instance to serialise.</param>
        /// <exception cref="ArgumentException">Thrown if the given buffer is too small.</exception>
        public static void SerialiseToBuffer(Memory<byte> buffer, NetworkPacket instance)
        {
            if (buffer.Length < PacketSize)
            {
                throw new ArgumentException("Given buffer is too small to serialise the packet instance into.", nameof(buffer));
            }

            Span<byte> serialisedPacketHeader = buffer.Slice(0, HeaderSize).Span;
            NetworkPacketHeader.Serialise(serialisedPacketHeader, instance.Header);

            Span<byte> serialisedPacketFooter = buffer.Slice(HeaderSize + DataSegmentSize, FooterSize).Span;
            NetworkPacketFooter.Serialise(serialisedPacketFooter, instance.Footer);

            Memory<byte> serialisedInstanceData = buffer.Slice(HeaderSize, DataSegmentSize);
            instance.DataBuffer.CopyTo(serialisedInstanceData);
        }
    }

    // TODO: Document
    public readonly struct NetworkPacketFooter
    {
        private const int PacketHasNextStart = 0;

        /// <summary>
        /// The number of bytes taken up by a packet footer.
        /// </summary>
        public const int Size = sizeof(bool);

        public readonly bool HasSucceedingPacket;

        public NetworkPacketFooter(bool hasSucceedingPacket)
        {
            HasSucceedingPacket = hasSucceedingPacket;
        }

        public static NetworkPacketFooter Deserialise(Span<byte> buffer)
        {
            Span<byte> serialisedHasNextFlag = buffer.Slice(PacketHasNextStart, sizeof(bool));

            return new NetworkPacketFooter(
                EndianAwareBitConverter.ToBoolean(serialisedHasNextFlag));
        }

        public static void Serialise(Span<byte> buffer, NetworkPacketFooter instance)
        {
            Span<byte> serialisedHasNextFlag = buffer.Slice(PacketHasNextStart, sizeof(bool));

            EndianAwareBitConverter.GetBytes(instance.HasSucceedingPacket).CopyTo(serialisedHasNextFlag);
        }
    }

    // TODO: Document
    public readonly struct NetworkPacketHeader
    {
        private const int PacketDataLengthStart = 2 * sizeof(uint);
        private const int PacketErrorCodeStart = sizeof(uint);
        private const int PacketTypeStart = 0;

        /// <summary>
        /// The number of bytes taken up by a packet header.
        /// </summary>
        public const int Size = sizeof(uint) + sizeof(uint) + sizeof(int);

        /// <summary>
        /// The number of bytes of data held in the packet.
        /// </summary>
        public readonly int DataLength;

        /// <summary>
        /// The error code for this packet.
        /// </summary>
        public readonly NetworkErrorCode ErrorCode;

        /// <summary>
        /// The packet type.
        /// </summary>
        public readonly uint Type;

        public NetworkPacketHeader(uint packetType, NetworkErrorCode packetErrorCode, int packetDataLength)
        {
            Type = packetType;
            ErrorCode = packetErrorCode;
            DataLength = packetDataLength;
        }

        public static NetworkPacketHeader Deserialise(Span<byte> buffer)
        {
            Span<byte> serialisedType = buffer.Slice(PacketTypeStart, sizeof(uint));
            Span<byte> serialisedErrorCode = buffer.Slice(PacketErrorCodeStart, sizeof(uint));
            Span<byte> serialisedDataLength = buffer.Slice(PacketDataLengthStart, sizeof(int));

            return new NetworkPacketHeader(
                EndianAwareBitConverter.ToUInt32(serialisedType),
                (NetworkErrorCode)EndianAwareBitConverter.ToUInt32(serialisedErrorCode),
                EndianAwareBitConverter.ToInt32(serialisedDataLength));
        }

        public static void Serialise(Span<byte> buffer, NetworkPacketHeader instance)
        {
            Span<byte> serialisedType = buffer.Slice(PacketTypeStart, sizeof(uint));
            Span<byte> serialisedErrorCode = buffer.Slice(PacketErrorCodeStart, sizeof(uint));
            Span<byte> serialisedDataLength = buffer.Slice(PacketDataLengthStart, sizeof(int));

            EndianAwareBitConverter.GetBytes(instance.Type).CopyTo(serialisedType);
            EndianAwareBitConverter.GetBytes((uint)instance.ErrorCode).CopyTo(serialisedErrorCode);
            EndianAwareBitConverter.GetBytes(instance.DataLength).CopyTo(serialisedDataLength);
        }
    }
}