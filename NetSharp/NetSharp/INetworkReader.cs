namespace NetSharp
{
    public interface INetworkReader
    {
        public void Start(ushort concurrentReadTasks);

        public void Stop();
    }
}