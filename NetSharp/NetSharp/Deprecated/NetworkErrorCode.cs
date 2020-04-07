namespace NetSharp.Deprecated
{
    /// <summary>
    /// Enumerates the possible error codes for network operations, being held in the packet.
    /// </summary>
    public enum NetworkErrorCode : uint
    {
        /// <summary>
        /// Signifies that there was no error during transmission.
        /// </summary>
        Ok = 0,

        /// <summary>
        /// A generic error occurred during packet transmission.
        /// </summary>
        Error = 1 << 1,
    }
}