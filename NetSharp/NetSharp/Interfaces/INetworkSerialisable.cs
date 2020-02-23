using System;

namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes an object that can be serialised to be sent across the network.
    /// </summary>
    public interface INetworkSerialisable
    {
        /// <summary>
        /// Deserialises the object instance from a byte array.
        /// </summary>
        /// <param name="serialisedObject">The memory containing the serialised object instance.</param>
        void Deserialise(ReadOnlyMemory<byte> serialisedObject);

        /// <summary>
        /// Serialises the object instance into a byte array.
        /// </summary>
        /// <returns>The memory containing the serialised object instance.</returns>
        Memory<byte> Serialise();
    }
}