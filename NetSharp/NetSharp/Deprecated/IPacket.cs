namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes the methods and properties that every packet
    /// </summary>
    public interface IPacket
    {
        /// <summary>
        /// Allows for custom fields to be converted from their serialised format, after being received from the network.
        /// </summary>
        void AfterDeserialisation();

        /// <summary>
        /// Allows for custom fields to be converted into another format prior to being sent via the network.
        /// </summary>
        void BeforeSerialisation();
    }
}