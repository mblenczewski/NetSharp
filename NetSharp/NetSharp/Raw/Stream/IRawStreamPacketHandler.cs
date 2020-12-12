namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Describes the interface for a stream network connection that can handle received packets.
    /// </summary>
    public interface IRawStreamPacketHandler
    {
        /// <summary>
        /// Deregisters a previously registered handler for the given packet type. No further invocations of the given
        /// handler will be made.
        /// </summary>
        /// <param name="id">
        /// The packet type for which to deregister the handler.
        /// </param>
        /// <param name="handler">
        /// The handler to deregister.
        /// </param>
        void DeregisterHandler(int id, RawStreamPacketHandler handler);

        /// <summary>
        /// Registers the given handler for the given packet type.
        /// </summary>
        /// <param name="id">
        /// The packet type for which to register the handler.
        /// </param>
        /// <param name="handler">
        /// The handler to register.
        /// </param>
        void RegisterHandler(int id, RawStreamPacketHandler handler);
    }
}
