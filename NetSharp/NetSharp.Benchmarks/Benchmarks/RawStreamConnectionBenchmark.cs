using System.Threading.Tasks;

namespace NetSharp.Benchmarks.Benchmarks
{
    public class RawStreamConnectionBenchmark : INetSharpBenchmark
    {
        /// <inheritdoc />
        public string Name { get; } = "Raw Stream Connection Benchmark";

        /// <inheritdoc />
        public Task RunAsync()
        {
            return Task.CompletedTask;
        }
    }
}
