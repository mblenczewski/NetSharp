using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using NetSharp.Logging;
using NetSharp.Utils;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Base class for connections, holding methods shared between the <see cref="Client"/> and <see cref="Server"/> classes.
    /// </summary>
    public abstract class ServerClientConnection : IDisposable
    {
        /// <summary>
        /// The logger to which the server can log messages.
        /// </summary>
        protected Logger logger;

        /// <summary>
        /// Initialises a new instance of the <see cref="ServerClientConnection"/> class.
        /// </summary>
        protected ServerClientConnection()
        {
            //networkManager = new NetworkOperationsManager();

            logger = new Logger(Stream.Null);
        }

        /// <summary>
        /// Disposes of this <see cref="ServerClientConnection"/> instance.
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