using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A response packet for the <see cref="ConnectPacket"/>.
    /// </summary>
    [PacketTypeId(2)]
    internal class ConnectResponsePacket : IResponsePacket<ConnectPacket>
    {
        /// <inheritdoc />
        public ConnectPacket RequestPacket { get; set; } = new ConnectPacket();

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