using System;

namespace NetSharp.Packets
{
    public readonly struct NetworkPacket
    {
        public const int TotalSize = HeaderSize + DataSize + FooterSize;

        public const int HeaderSize = NetworkPacketHeader.TotalSize;

        public const int FooterSize = NetworkPacketFooter.TotalSize;

        public const int DataSize = 8192;

        public readonly ReadOnlyMemory<byte> Data;

        public readonly NetworkPacketHeader Header;

        public readonly NetworkPacketFooter Footer;

        /// <summary>
        /// Constructs a new instance of the <see cref="NetworkPacket"/> struct.
        /// </summary>
        /// <param name="packetHeader">The header for this packet.</param>
        /// <param name="packetDataBuffer">The data that should be stored in the packet.</param>
        /// <param name="packetFooter">The footer for this packet.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when the given <paramref name="packetDataBuffer"/> exceeds <see cref="TotalSize"/> bytes in size.
        /// </exception>
        private NetworkPacket(NetworkPacketHeader packetHeader, ReadOnlyMemory<byte> packetDataBuffer, NetworkPacketFooter packetFooter)
        {
            if (packetDataBuffer.Length > DataSize)
            {
                throw new ArgumentException(
                    $"Given buffer exceeds {TotalSize} bytes, and cannot fit into a network packet",
                    nameof(packetDataBuffer));
            }

            Data = packetDataBuffer;
        }

        public static NetworkPacket Deserialise(ReadOnlyMemory<byte> buffer)
        {
            ReadOnlyMemory<byte> serialisedPacketHeader = buffer.Slice(0, HeaderSize);
            NetworkPacketHeader packetHeader = NetworkPacketHeader.Deserialise(serialisedPacketHeader);

            ReadOnlyMemory<byte> packetDataBuffer = buffer.Slice(HeaderSize, DataSize);

            ReadOnlyMemory<byte> serialisedPacketFooter = buffer.Slice(HeaderSize + DataSize, FooterSize);
            NetworkPacketFooter packetFooter = NetworkPacketFooter.Deserialise(serialisedPacketFooter);

            return new NetworkPacket(packetHeader, packetDataBuffer, packetFooter);
        }

        public static void Serialise(NetworkPacket instance, Memory<byte> buffer)
        {
            Memory<byte> packetHeader = buffer.Slice(0, HeaderSize);
            NetworkPacketHeader.Serialise(instance.Header, packetHeader);

            Memory<byte> packetDataBuffer = buffer.Slice(HeaderSize, DataSize);
            instance.Data.CopyTo(packetDataBuffer);

            Memory<byte> packetFooter = buffer.Slice(HeaderSize + DataSize, FooterSize);
            NetworkPacketFooter.Serialise(instance.Footer, packetFooter);
        }

        public readonly struct NetworkPacketHeader
        {
            public const int TotalSize = 0;

            public static NetworkPacketHeader Deserialise(ReadOnlyMemory<byte> buffer)
            {
                return new NetworkPacketHeader();
            }

            public static void Serialise(NetworkPacketHeader instance, Memory<byte> buffer)
            {
            }
        }

        public readonly struct NetworkPacketFooter
        {
            public const int TotalSize = 0;

            public static NetworkPacketFooter Deserialise(ReadOnlyMemory<byte> buffer)
            {
                return new NetworkPacketFooter();
            }

            public static void Serialise(NetworkPacketFooter instance, Memory<byte> buffer)
            {
            }
        }
    }
}