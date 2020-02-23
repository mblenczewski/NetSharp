using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A simple disconnect packet for the UDP protocol.
    /// </summary>
    [PacketTypeId(0)]
    internal class DisconnectPacket : IRequestPacket
    {
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