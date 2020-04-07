using System;

namespace NetSharp.Deprecated.Builtin
{
    /// <summary>
    /// A simple one-time-use data transfer packet, that allows for the transmission of an arbitrary number of frames.
    /// </summary>
    [PacketTypeId(7)]
    public class SimpleDataPacket : IRequestPacket
    {
        /// <summary>
        /// The data that should be transferred across the network.
        /// </summary>
        public Memory<byte> RequestBuffer;

        /// <summary>
        /// Initialises a new instance of the <see cref="SimpleDataPacket"/> class.
        /// </summary>
        public SimpleDataPacket()
        {
            RequestBuffer = new byte[0];
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="SimpleDataPacket"/> class.
        /// </summary>
        /// <param name="buffer">The data that this request packet should contain.</param>
        public SimpleDataPacket(Memory<byte> buffer)
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