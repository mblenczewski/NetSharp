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
        public ReadOnlyMemory<byte> Serialise()
        {
            return ReadOnlyMemory<byte>.Empty;
        }
    }
}