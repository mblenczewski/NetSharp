using System.Threading.Tasks;

namespace NetSharpExamples
{
    /// <summary>
    /// Defines an example program.
    /// </summary>
    public interface INetSharpExample
    {
        /// <summary>
        /// Runs the example asynchronously.
        /// </summary>
        Task RunAsync();
    }
}