using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A response packet for the <see cref="PingPacket"/>.
    /// </summary>
    [PacketTypeId(4)]
    public class PingResponsePacket : IResponsePacket<PingPacket>
    {
        /// <inheritdoc />
        public PingPacket RequestPacket { get; internal set; } = new PingPacket();

        /// <inheritdoc />
        public void AfterDeserialisation()
        {
        }

        /// <inheritdoc />
        public void BeforeSerialisation()
        {
        }

        /// <inheritdoc />
        public void Deserialise(ReadOnlyMemory<byte> serialisedObject)
        {
        }

        /// <inheritdoc />
        public Memory<byte> Serialise()
        {
            return Memory<byte>.Empty;
        }
    }
}