using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Interfaces;
using NetSharp.Logging;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharp
{
    /// <summary>
    /// Base class for connections, holding methods shared between the <see cref="Client"/> and <see cref="Server"/> classes.
    /// </summary>
    public abstract class Connection : IDisposable
    {
        /// <summary>
        /// The logger to which the server can log messages.
        /// </summary>
        protected Logger logger;

        /// <summary>
        /// Initialises a new instance of the <see cref="Connection"/> class.
        /// </summary>
        protected Connection()
        {
            logger = new Logger(Stream.Null);
        }

        /// <summary>
        /// Disposes of this <see cref="Connection"/> instance.
        /// </summary>
        /// <param name="disposing">Whether this instance is being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                logger.Dispose();
            }
        }

        /// <summary>
        /// Listens for a packet to be received from the network.
        /// </summary>
        /// <param name="socket">The remote socket from which to receive data.</param>
        /// <param name="socketFlags">The socket flags associated with the read operation.</param>
        /// <param name="timeout">
        /// The timespan within which the packet should be received. After this timespan elapses, the receive task is cancelled.
        /// </param>
        /// <param name="cancellationToken">A pre-existing cancellation token that should be observed alongside the timeout.</param>
        /// <returns>The packet that was received. <see cref="NullPacket"/> if not received correctly.</returns>
        protected async Task<SerialisedPacket> DoReceivePacketAsync(Socket socket, SocketFlags socketFlags, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

            try
            {
                (SerialisedPacket packet, EndPoint endPoint) =
                    await NetworkOperations.ReadPacketAsync(socket, socketFlags, cts.Token);

                OnBytesReceived(endPoint, packet.Contents.Length);

                return packet;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception while reading bytes from {socket.RemoteEndPoint}:", ex);
                return SerialisedPacket.Null;
            }
            catch (Exception ex)
            {
                logger.LogException($"Exception while reading bytes from {socket.RemoteEndPoint}:", ex);
                return SerialisedPacket.Null;
            }
        }

        /// <summary>
        /// Listens for a packet to be received from the network.
        /// </summary>
        /// <param name="socket">The socket which will receive the packet.</param>
        /// <param name="remoteEndPoint">The remote endpoint from which to receive the packet.</param>
        /// <param name="socketFlags">The socket flags associated with the read operation.</param>
        /// <param name="timeout">
        /// The timespan within which the packet should be received. After this timespan elapses, the receive task is cancelled.
        /// </param>
        /// <param name="cancellationToken">A pre-existing cancellation token that should be observed alongside the timeout.</param>
        /// <returns>
        /// The packet that was received and the associated transmission result. <see cref="NullPacket"/> if not received correctly.
        /// </returns>
        protected async Task<(SerialisedPacket packet, EndPoint remoteEndPoint)> DoReceivePacketFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

            try
            {
                (SerialisedPacket packet, EndPoint endPoint) =
                    await NetworkOperations.ReadPacketFromAsync(socket, remoteEndPoint, socketFlags, cts.Token);

                OnBytesReceived(endPoint, packet.Contents.Length);

                return (packet, remoteEndPoint);
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception while reading bytes from {remoteEndPoint}:", ex);
                return (SerialisedPacket.Null, new IPEndPoint(IPAddress.None, IPEndPoint.MinPort));
            }
            catch (Exception ex)
            {
                logger.LogException($"Exception while reading bytes from {remoteEndPoint}:", ex);
                return (SerialisedPacket.Null, new IPEndPoint(IPAddress.None, IPEndPoint.MinPort));
            }
        }

        /// <summary>
        /// Sends the given packet to the network.
        /// </summary>
        /// <param name="remoteSocket">The remote socket to which to send the packet.</param>
        /// <param name="packet">The packet to send.</param>
        /// <param name="socketFlags">The socket flags associated with the write operation.</param>
        /// <param name="timeout">
        /// The timespan within which the packet should be received. After this timespan elapses, the send task is cancelled.
        /// </param>
        /// <param name="cancellationToken">A pre-existing cancellation token that should be observed alongside the timeout.</param>
        /// <returns>Whether the packet was successfully sent.</returns>
        protected async Task<bool> DoSendPacketAsync(Socket remoteSocket, SerialisedPacket packet, SocketFlags socketFlags, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

            try
            {
                await NetworkOperations.WritePacketAsync(remoteSocket, packet, socketFlags, cts.Token);

                OnBytesSent(remoteSocket.RemoteEndPoint, packet.Contents.Length);

                return true;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception while sending bytes to {remoteSocket.RemoteEndPoint}:", ex);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogException($"Exception while sending bytes to {remoteSocket.RemoteEndPoint}:", ex);
                return false;
            }
        }

        /// <summary>
        /// Sends the given packet to the network.
        /// </summary>
        /// <param name="socket">The socket which should send the packet.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which to send the packet.</param>
        /// <param name="packet">The packet to send.</param>
        /// <param name="socketFlags">The socket flags associated with the write operation.</param>
        /// <param name="timeout">
        /// The timespan within which the packet should be received. After this timespan elapses, the send task is cancelled.
        /// </param>
        /// <param name="cancellationToken">A pre-existing cancellation token that should be observed alongside the timeout.</param>
        /// <returns>Whether the packet was successfully sent.</returns>
        protected async Task<bool> DoSendPacketToAsync(Socket socket, EndPoint remoteEndPoint, SerialisedPacket packet, SocketFlags socketFlags,
            TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using CancellationTokenSource cts =
                CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

            try
            {
                await NetworkOperations.WritePacketToAsync(socket, remoteEndPoint, packet, socketFlags, cts.Token);

                OnBytesSent(remoteEndPoint, packet.Contents.Length);

                return true;
            }
            catch (SocketException ex)
            {
                logger.LogException($"Socket exception while sending bytes to {remoteEndPoint}:", ex);
                return false;
            }
            catch (Exception ex)
            {
                logger.LogException($"Exception while sending bytes to {remoteEndPoint}:", ex);
                return false;
            }
        }

        /// <summary>
        /// Invokes the <see cref="BytesReceived"/> event.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint from which the bytes were received.</param>
        /// <param name="bytesReceived">The number of bytes that were received from the remote endpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnBytesReceived(EndPoint remoteEndPoint, int bytesReceived) =>
            BytesReceived?.Invoke(remoteEndPoint, bytesReceived);

        /// <summary>
        /// Invokes the <see cref="BytesSent"/> event.
        /// </summary>
        /// <param name="remoteEndPoint">The remote endpoint to which the bytes were sent.</param>
        /// <param name="bytesSent">The number of bytes that were sent to the remote endpoint.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void OnBytesSent(EndPoint remoteEndPoint, int bytesSent) =>
            BytesSent?.Invoke(remoteEndPoint, bytesSent);

        /// <summary>
        /// Signifies that some data has been received from the remote endpoint.
        /// </summary>
        public event Action<EndPoint, int>? BytesReceived;

        /// <summary>
        /// Signifies that some data was sent to the remote endpoint.
        /// </summary>
        public event Action<EndPoint, int>? BytesSent;

        /// <summary>
        /// Makes the client log to the given stream.
        /// </summary>
        /// <param name="loggingStream">The stream that new messages should be logged to.</param>
        /// <param name="minimumMessageSeverityLevel">
        /// The minimum severity level that new messages must have to be logged to the stream.
        /// </param>
        public void ChangeLoggingStream(Stream loggingStream, LogLevel minimumMessageSeverityLevel = LogLevel.Info)
        {
            logger = new Logger(loggingStream, minimumMessageSeverityLevel);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}