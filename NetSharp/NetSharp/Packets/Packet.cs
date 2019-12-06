using System;
using NetSharp.Utils.Conversion;

namespace NetSharp.Packets
{
    /// <summary>
    /// Represents a packet that is transmitted over the network.
    /// </summary>
    public readonly struct Packet
    {
        /// <summary>
        /// The size of the packet header in bytes.
        /// </summary>
        public static readonly int HeaderSize = sizeof(int) + sizeof(uint) + sizeof(uint);

        /// <summary>
        /// The data held in this packet.
        /// </summary>
        public readonly ReadOnlyMemory<byte> Buffer;

        /// <summary>
        /// The size of the packet's data.
        /// </summary>
        public readonly int Count;

        /// <summary>
        /// The error code for this packet.
        /// </summary>
        public readonly NetworkErrorCode ErrorCode;

        /// <summary>
        /// The packet type.
        /// </summary>
        public readonly uint Type;

        /// <summary>
        /// Initialises a new instance of the <see cref="Packet"/> struct.
        /// </summary>
        /// <param name="data">The data that should be transmitted in the packet.</param>
        /// <param name="type">The packet type.</param>
        /// <param name="errorCode">The error code associated with this transmission.</param>
        public Packet(ReadOnlyMemory<byte> data, uint type, NetworkErrorCode errorCode)
        {
            Buffer = data;
            Count = data.Length;
            Type = type;

            ErrorCode = errorCode;
        }

        /// <summary>
        /// Returns the total size of the serialised packet (including the header) in bytes.
        /// </summary>
        /// <returns>The total size of the serialised packet (including the header) in bytes.</returns>
        public int TotalSize { get { return HeaderSize + Count; } }

        /// <summary>
        /// Deserialises the given buffer into a packet instance.
        /// </summary>
        /// <param name="buffer">The byte buffer to serialise.</param>
        /// <returns>The deserialised packet instance.</returns>
        public static Packet Deserialise(Memory<byte> buffer)
        {
            Span<byte> serialisedType = buffer.Slice(sizeof(int), sizeof(uint)).Span;
            Span<byte> serialisedErrorCode = buffer.Slice(sizeof(int) + sizeof(uint), sizeof(uint)).Span;
            Memory<byte> serialisedData = buffer.Slice(HeaderSize);

            return new Packet(serialisedData,
                EndianAwareBitConverter.ToUInt32(serialisedType),
                (NetworkErrorCode)EndianAwareBitConverter.ToUInt32(serialisedErrorCode));
        }

        /// <summary>
        /// Serialises the given packet instance into a single byte buffer.
        /// </summary>
        /// <param name="instance">The packet instance to serialise.</param>
        /// <returns>The byte buffer that represents the packet instance.</returns>
        public static Memory<byte> Serialise(Packet instance)
        {
            byte[] buffer = new byte[HeaderSize + instance.Count];

            Span<byte> serialisedInstanceLength = new Span<byte>(buffer, 0, sizeof(int));
            EndianAwareBitConverter.GetBytes(instance.Count).CopyTo(serialisedInstanceLength);

            Span<byte> serialisedInstanceType = new Span<byte>(buffer, sizeof(int), sizeof(uint));
            EndianAwareBitConverter.GetBytes(instance.Type).CopyTo(serialisedInstanceType);

            Span<byte> serialisedErrorCode = new Span<byte>(buffer, sizeof(int) + sizeof(uint), sizeof(uint));
            EndianAwareBitConverter.GetBytes((uint)instance.ErrorCode).CopyTo(serialisedErrorCode);

            Memory<byte> serialisedInstanceData = new Memory<byte>(buffer, HeaderSize, instance.Count);
            instance.Buffer.CopyTo(serialisedInstanceData);

            return new Memory<byte>(buffer);
        }
    }
}