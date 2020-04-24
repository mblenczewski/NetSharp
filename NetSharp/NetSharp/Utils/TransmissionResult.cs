using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NetSharp.Utils
{
    /// <summary>
    /// Represents the result of a socket transmission.
    /// </summary>
    public readonly struct TransmissionResult
    {
        /// <summary>
        /// Represents an asynchronous transmission which timed out.
        /// </summary>
        internal static readonly TransmissionResult Timeout = new TransmissionResult();

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
        /// Initialises a new instance of the <see cref="TransmissionResult" /> struct.
        /// </summary>
        /// <param name="args">
        /// The socket arguments associated with the transmission.
        /// </param>
        internal TransmissionResult(in SocketAsyncEventArgs args)
        {
            Buffer = args.MemoryBuffer;
            Count = args.BytesTransferred;
            RemoteEndPoint = args.RemoteEndPoint;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="TransmissionResult" /> struct.
        /// </summary>
        /// <param name="buffer">
        /// The buffer associated with the transmission.
        /// </param>
        /// <param name="count">
        /// The number of bytes written to or read from the buffer.
        /// </param>
        /// <param name="remoteEndPoint">
        /// The remote end point associated with the transmission.
        /// </param>
        internal TransmissionResult(in byte[] buffer, in int count, in EndPoint remoteEndPoint)
        {
            Buffer = buffer;
            Count = count;
            RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// Checks whether this instance represents a timed out transmission.
        /// </summary>
        /// <returns>
        /// Whether this instance has timed out.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TimedOut() => Equals(Timeout);
    }
}