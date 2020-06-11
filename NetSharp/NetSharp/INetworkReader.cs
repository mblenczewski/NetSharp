namespace NetSharp
{
    public interface INetworkReader
    {
        public void Shutdown();

        public void Start(ushort concurrentTasks);
    }
}