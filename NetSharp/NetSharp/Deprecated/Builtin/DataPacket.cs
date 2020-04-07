using System;

namespace NetSharp.Deprecated.Builtin
{
    /// <summary>
    /// A simple data transfer packet, that allows for the transmission of an arbitrary number of frames.
    /// </summary>
    [PacketTypeId(5)]
    public class DataPacket : IRequestPacket
    {
        /// <summary>
        /// The data that should be transferred across the network.
        /// </summary>
        public Memory<byte> RequestBuffer;

        /// <summary>
        /// Initialises a new instance of the <see cref="DataPacket"/> class.
        /// </summary>
        public DataPacket()
        {
            RequestBuffer = new byte[0];
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="DataPacket"/> class.
        /// </summary>
        /// <param name="buffer">The data that this request packet should contain.</param>
        public DataPacket(Memory<byte> buffer)
        {
            RequestBuffer = buffer;
        }

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
            RequestBuffer = serialisedObject.ToArray();
        }

        /// <inheritdoc />
        public Memory<byte> Serialise()
        {
            return RequestBuffer;
        }
    }
}