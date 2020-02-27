using System;
using NetSharp.Deprecated;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A simple ping request packet for heartbeat monitoring and RTT measurement.
    /// </summary>
    [PacketTypeId(3)]
    public class PingPacket : IRequestPacket
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