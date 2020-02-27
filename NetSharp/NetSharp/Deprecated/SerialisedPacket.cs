using System;
using NetSharp.Interfaces;

namespace NetSharp.Packets
{
    public readonly struct SerialisedPacket
    {
        public static readonly SerialisedPacket Null = new SerialisedPacket(Memory<byte>.Empty, 0);
        public readonly Memory<byte> Contents;
        public readonly uint Type;

        public SerialisedPacket(Memory<byte> contents, uint type)
        {
            Contents = contents;

            Type = type;
        }

        /// <summary>
        /// Serialises the given serialisable packet instance and returns the <see cref="SerialisedPacket"/> instance
        /// that was generated. This method invokes <see cref="IPacket.BeforeSerialisation"/>.
        /// </summary>
        /// <typeparam name="T">The packet type that will be serialised.</typeparam>
        /// <param name="serialisable">The packet instance that should be serialised.</param>
        /// <returns>The serialised instance.</returns>
        public static SerialisedPacket From<T>(T serialisable) where T : class, IPacket, INetworkSerialisable
        {
            serialisable.BeforeSerialisation();
            return new SerialisedPacket(serialisable.Serialise(), PacketRegistry.GetPacketId<T>());
        }

        /// <summary>
        /// Deserialises and returns a packet instance of the given type from the <see cref="SerialisedPacket"/> instance
        /// that was given. This method invokes <see cref="IPacket.AfterDeserialisation"/>.
        /// </summary>
        /// <typeparam name="T">The packet type to which the packet should be deserialised.</typeparam>
        /// <param name="instance">The serialised packet instance that should be deserialised.</param>
        /// <returns>The deserialised instance.</returns>
        public static T To<T>(in SerialisedPacket instance) where T : class, IPacket, INetworkSerialisable, new()
        {
            T packet = new T();
            packet.Deserialise(instance.Contents);
            packet.AfterDeserialisation();

            return packet;
        }
    }
}