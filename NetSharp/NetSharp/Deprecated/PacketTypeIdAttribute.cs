using System;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Allows the placing of a custom packet type on a class or struct. This is used if the class or struct
    /// inherits from <see cref="IRequestPacket"/> or <see cref="IResponsePacket{TReq}"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    internal sealed class PacketTypeIdAttribute : Attribute
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="PacketTypeIdAttribute"/> attribute.
        /// </summary>
        /// <param name="type">The custom type id that the decorated packet type should have.</param>
        internal PacketTypeIdAttribute(uint type)
        {
            Id = type;
        }

        /// <summary>
        /// The custom type id that the decorated packet type should have. This overrides the automatically generated id.
        /// </summary>
        internal uint Id { get; }
    }
}