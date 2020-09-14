namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes the methods and properties that a raw network reader (i.e. binary server) must implement.
    /// </summary>
    public interface IRawNetworkReader
    {
        /// <summary>
        /// Stops queuing up new asynchronous read operations. Does not terminate existing asynchronous operations; the underlying socket must be
        /// terminated to cancel all current 'in-flight' asynchronous operations.
        /// </summary>
        public void Shutdown();

        /// <summary>
        /// Queues up <paramref name="concurrentTasks" /> new asynchronous read operations.
        /// </summary>
        /// <param name="concurrentTasks">
        /// The number of asynchronous read operation to queue up.
        /// </param>
        public void Start(ushort concurrentTasks);
    }
}
