namespace NetSharp.Deprecated
{
    /// <summary>
    /// Describes a response packet to a request packet.
    /// </summary>
    /// <typeparam name="TReq">The request packet that this type is a response to.</typeparam>
    public interface IResponsePacket<out TReq> : IPacket, INetworkSerialisable where TReq : IRequestPacket
    {
        /// <summary>
        /// The request packet that was handled with this response packet.
        /// </summary>
        TReq RequestPacket { get; }
    }
}