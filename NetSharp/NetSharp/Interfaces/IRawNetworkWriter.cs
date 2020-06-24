using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Interfaces
{
    /// <summary>
    /// Describes the methods and properties that a raw network writer (i.e. binary client) must implement.
    /// </summary>
    public interface IRawNetworkWriter
    {
        /// <summary>
        /// Reads bytes from the network until either the given <paramref name="readBuffer" /> is filled, or a single datagram has been received.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint from which to receive bytes. Ignored for stream connections, where the connected socket's remote endpoint is used instead.
        /// </param>
        /// <param name="readBuffer">
        /// The buffer into which to place any received bytes.
        /// </param>
        /// <param name="flags">
        /// The <see cref="SocketFlags" /> associated with the network operation.
        /// </param>
        /// <returns>
        /// The number of bytes of data read from the remote connection.
        /// </returns>
        public int Read(ref EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Asynchronously reads bytes from the network until either the given <paramref name="readBuffer" /> is filled, or a single datagram has been received.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint from which to receive bytes. Ignored for stream connections, where the connected socket's remote endpoint is used instead.
        /// </param>
        /// <param name="readBuffer">
        /// The buffer into which to place any received bytes.
        /// </param>
        /// <param name="flags">
        /// The <see cref="SocketFlags" /> associated with the network operation.
        /// </param>
        /// <returns>
        /// The number of bytes of data read from the remote connection.
        /// </returns>
        public ValueTask<int> ReadAsync(EndPoint remoteEndPoint, Memory<byte> readBuffer, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Writes bytes in the given <paramref name="writeBuffer" /> to the network. If using a datagram connection, make sure that the
        /// <paramref name="writeBuffer" /> size doesn't exceed the max datagram size for that transport.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint to which to send bytes. Ignored for stream connections, where the connected socket's remote endpoint is used instead.
        /// </param>
        /// <param name="writeBuffer">
        /// The buffer whose contents to send.
        /// </param>
        /// <param name="flags">
        /// The <see cref="SocketFlags" /> associated with the network operation.
        /// </param>
        /// <returns>
        /// The number of bytes of data sent to the remote endpoint.
        /// </returns>
        public int Write(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None);

        /// <summary>
        /// Asynchronously writes bytes in the given <paramref name="writeBuffer" /> to the network. If using a datagram connection, make sure that
        /// the <paramref name="writeBuffer" /> size doesn't exceed the max datagram size for that transport.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The remote endpoint to which to send bytes. Ignored for stream connections, where the connected socket's remote endpoint is used instead.
        /// </param>
        /// <param name="writeBuffer">
        /// The buffer whose contents to send.
        /// </param>
        /// <param name="flags">
        /// The <see cref="SocketFlags" /> associated with the network operation.
        /// </param>
        /// <returns>
        /// The number of bytes of data sent to the remote endpoint.
        /// </returns>
        public ValueTask<int> WriteAsync(EndPoint remoteEndPoint, ReadOnlyMemory<byte> writeBuffer, SocketFlags flags = SocketFlags.None);
    }
}