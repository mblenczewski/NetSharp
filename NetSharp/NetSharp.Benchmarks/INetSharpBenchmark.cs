using System.Threading.Tasks;

namespace NetSharp.Benchmarks
{
    /// <summary>
    /// Defines a benchmark program.
    /// </summary>
    public interface INetSharpBenchmark
    {
        /// <summary>
        /// The name of the benchmark.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Runs the benchmark asynchronously.
        /// </summary>
        Task RunAsync();
    }
}
