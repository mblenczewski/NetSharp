using System;
using System.Net;

namespace NetSharp.Utils
{
    /// <summary>
    /// Represents the result of a socket transmission.
    /// </summary>
    public readonly struct TransmissionResult
    {
        /// <summary>
        /// The byte buffer that was transmitted across the network.
        /// </summary>
        public readonly Memory<byte> Buffer;

        /// <summary>
        /// The number of bytes that were transmitted across the network.
        /// </summary>
        public readonly int Count;

        /// <summary>
        /// The remote endpoint to which the buffer was transmitted.
        /// </summary>
        public readonly EndPoint RemoteEndPoint;

        /// <summary>
        /// Initialises a new instance of the <see cref="TransmissionResult"/> struct.
        /// </summary>
        /// <param name="buffer">The byte buffer that was transmitted.</param>
        /// <param name="count">The number of bytes that were transmitted.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which the buffer was transmitted.</param>
        public TransmissionResult(Memory<byte> buffer, int count, EndPoint remoteEndPoint)
        {
            Buffer = buffer;
            Count = count;
            RemoteEndPoint = remoteEndPoint;
        }
    }
}