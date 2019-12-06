using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets.Builtin
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
        public ReadOnlyMemory<byte> RequestBuffer;

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
        public SimpleDataPacket(ReadOnlyMemory<byte> buffer)
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
            RequestBuffer = serialisedObject;
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> Serialise()
        {
            return RequestBuffer;
        }
    }
}