using System;
using System.Runtime.CompilerServices;

namespace NetSharp.Packets
{
    /// <summary>
    /// Represents a raw packet sent across the network.
    /// </summary>
    public readonly struct NetworkPacket
    {
        /// <summary>
        /// The size in bytes of the packet data segment.
        /// </summary>
        public const int DataSize = 8192;

        /// <summary>
        /// The size in bytes of the packet footer segment.
        /// </summary>
        public const int FooterSize = NetworkPacketFooter.TotalSize;

        /// <summary>
        /// The size in bytes of the packet header segment.
        /// </summary>
        public const int HeaderSize = NetworkPacketHeader.TotalSize;

        /// <summary>
        /// The total size of the packet in bytes.
        /// </summary>
        public const int TotalSize = HeaderSize + DataSize + FooterSize;

        /// <summary>
        /// Represents an empty packet.
        /// </summary>
        public static NetworkPacket NullPacket = new NetworkPacket();

        /// <summary>
        /// The data held by this packet instance.
        /// </summary>
        public readonly ReadOnlyMemory<byte> Data;

        /// <summary>
        /// The footer for this packet instance, holding additional metadata.
        /// </summary>
        public readonly NetworkPacketFooter Footer;

        /// <summary>
        /// The header for this packet instance, holding additional metadata.
        /// </summary>
        public readonly NetworkPacketHeader Header;

        /// <summary>
        /// Constructs a new instance of the <see cref="NetworkPacket" /> struct.
        /// </summary>
        /// <param name="packetHeader">
        /// The header for this packet.
        /// </param>
        /// <param name="packetDataBuffer">
        /// The data that should be stored in the packet.
        /// </param>
        /// <param name="packetFooter">
        /// The footer for this packet.
        /// </param>
        private NetworkPacket(NetworkPacketHeader packetHeader, ReadOnlyMemory<byte> packetDataBuffer, NetworkPacketFooter packetFooter)
        {
            Header = packetHeader;

            Data = packetDataBuffer;

            Footer = packetFooter;
        }

        /// <summary>
        /// Deserialises the serialised packet in the given memory buffer into a new <see cref="NetworkPacket" /> instance.
        /// </summary>
        /// <param name="buffer">
        /// The memory buffer to read the serialised packet instance from.
        /// </param>
        /// <param name="instance">
        /// The deserialised instance.
        /// </param>
        /// <returns>
        /// Whether the deserialisation attempt was successful. The <paramref name="instance" /> will be equal to <see cref="NullPacket" /> if the
        /// attempt fails.
        /// </returns>
        public static bool Deserialise(ReadOnlyMemory<byte> buffer, out NetworkPacket instance)
        {
            if (buffer.Length != TotalSize)
            {
                instance = NullPacket;

                return false;
            }

            ReadOnlyMemory<byte> serialisedPacketHeader = buffer.Slice(0, HeaderSize);
            NetworkPacketHeader packetHeader = NetworkPacketHeader.Deserialise(serialisedPacketHeader);

            ReadOnlyMemory<byte> packetDataBuffer = buffer.Slice(HeaderSize, DataSize);

            ReadOnlyMemory<byte> serialisedPacketFooter = buffer.Slice(HeaderSize + DataSize, FooterSize);
            NetworkPacketFooter packetFooter = NetworkPacketFooter.Deserialise(serialisedPacketFooter);

            instance = new NetworkPacket(packetHeader, packetDataBuffer, packetFooter);

            return true;
        }

        /// <summary>
        /// Serialises the given <see cref="NetworkPacket" /> instance into the given memory buffer.
        /// </summary>
        /// <param name="instance">
        /// The packet instance which should be serialised.
        /// </param>
        /// <param name="buffer">
        /// The memory buffer to write the serialised packet instance to. <see cref="TotalSize" /> bytes will be written into this buffer on success.
        /// </param>
        /// <returns>
        /// Whether the serialisation attempt was successful. No bytes are written to the <paramref name="buffer" /> if the attempt fails.
        /// </returns>
        public static bool Serialise(NetworkPacket instance, Memory<byte> buffer)
        {
            if (buffer.Length < TotalSize)
            {
                return false;
            }

            Memory<byte> packetHeader = buffer.Slice(0, HeaderSize);
            NetworkPacketHeader.Serialise(instance.Header, packetHeader);

            Memory<byte> packetDataBuffer = buffer.Slice(HeaderSize, DataSize);
            instance.Data.CopyTo(packetDataBuffer);

            Memory<byte> packetFooter = buffer.Slice(HeaderSize + DataSize, FooterSize);
            NetworkPacketFooter.Serialise(instance.Footer, packetFooter);

            return true;
        }
    }

    /// <summary>
    /// Represents the footer of a <see cref="NetworkPacket" />, holding additional metadata.
    /// </summary>
    public readonly struct NetworkPacketFooter
    {
        /// <summary>
        /// The total size of the packet footer, in bytes.
        /// </summary>
        public const int TotalSize = 0;

        /// <summary>
        /// Deserialises the serialised packet footer in the given memory buffer into a new <see cref="NetworkPacketFooter" /> instance.
        /// </summary>
        /// <param name="buffer">
        /// The memory buffer to read the serialised packet footer instance from.
        /// </param>
        /// <returns>
        /// The deserialised instance.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NetworkPacketFooter Deserialise(ReadOnlyMemory<byte> buffer)
        {
            return new NetworkPacketFooter();
        }

        /// <summary>
        /// Serialises the given <see cref="NetworkPacketFooter" /> instance into the given memory buffer.
        /// </summary>
        /// <param name="instance">
        /// The packet footer instance which should be serialised.
        /// </param>
        /// <param name="buffer">
        /// The memory buffer to write the serialised packet footer instance to.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialise(NetworkPacketFooter instance, Memory<byte> buffer)
        {
        }
    }

    /// <summary>
    /// Represents the header of a <see cref="NetworkPacket" />, holding additional metadata.
    /// </summary>
    public readonly struct NetworkPacketHeader
    {
        /// <summary>
        /// The total size of the packet header, in bytes.
        /// </summary>
        public const int TotalSize = 0;

        /// <summary>
        /// Deserialises the serialised packet header in the given memory buffer into a new <see cref="NetworkPacketHeader" /> instance.
        /// </summary>
        /// <param name="buffer">
        /// The memory buffer to read the serialised packet header instance from.
        /// </param>
        /// <returns>
        /// The deserialised instance.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static NetworkPacketHeader Deserialise(ReadOnlyMemory<byte> buffer)
        {
            return new NetworkPacketHeader();
        }

        /// <summary>
        /// Serialises the given <see cref="NetworkPacketHeader" /> instance into the given memory buffer.
        /// </summary>
        /// <param name="instance">
        /// The packet header instance which should be serialised.
        /// </param>
        /// <param name="buffer">
        /// The memory buffer to write the serialised packet header instance to.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialise(NetworkPacketHeader instance, Memory<byte> buffer)
        {
        }
    }
}