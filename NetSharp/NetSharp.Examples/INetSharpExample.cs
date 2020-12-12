using System.Threading.Tasks;

namespace NetSharp.Examples
{
    /// <summary>
    /// Defines an example program.
    /// </summary>
    public interface INetSharpExample
    {
        /// <summary>
        /// The name of the example.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Runs the example asynchronously.
        /// </summary>
        Task RunAsync();
    }
}
