using System;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Utils
{
    /// <summary>
    /// Represents the result of a socket transmission.
    /// </summary>
    public readonly struct TransmissionResult
    {
        /// <summary>
        /// Initialises a new instance of the <see cref="TransmissionResult"/> struct.
        /// </summary>
        /// <param name="args">The socket arguments associated with the transmission.</param>
        internal TransmissionResult(SocketAsyncEventArgs args)
        {
            TransmissionArgs = args;
            Buffer = args.MemoryBuffer;
            Count = args.BytesTransferred;
            RemoteEndPoint = args.RemoteEndPoint;
        }

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
        /// Socket arguments and other data associated with the transmission.
        /// </summary>
        public readonly SocketAsyncEventArgs TransmissionArgs;
    }
}