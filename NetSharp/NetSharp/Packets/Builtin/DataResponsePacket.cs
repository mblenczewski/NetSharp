using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
{
    /// <summary>
    /// A response packet for the <see cref="DataPacket"/>.
    /// </summary>
    [PacketTypeId(6)]
    public class DataResponsePacket : IResponsePacket<DataPacket>
    {
        /// <summary>
        /// The data that should be transferred across the network.
        /// </summary>
        public Memory<byte> ResponseBuffer;

        /// <summary>
        /// Initialises a new instance of the <see cref="DataResponsePacket"/> class.
        /// </summary>
        public DataResponsePacket()
        {
            ResponseBuffer = new byte[0];
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="DataResponsePacket"/> class.
        /// </summary>
        /// <param name="buffer">The data that this response packet should contain.</param>
        public DataResponsePacket(Memory<byte> buffer)
        {
            ResponseBuffer = buffer;
        }

        /// <inheritdoc />
        public DataPacket RequestPacket { get; internal set; } = new DataPacket();

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
            ResponseBuffer = serialisedObject.ToArray();
        }

        /// <inheritdoc />
        public Memory<byte> Serialise()
        {
            return ResponseBuffer;
        }
    }
}