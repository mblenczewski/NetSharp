using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A simple connection request packet for the UDP protocol.
    /// </summary>
    [PacketTypeId(1)]
    internal class ConnectPacket : IRequestPacket
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