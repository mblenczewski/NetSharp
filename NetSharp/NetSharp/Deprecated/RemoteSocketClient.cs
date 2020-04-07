using Microsoft.Extensions.ObjectPool;

using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
{
    public class RemoteSocketClient : IDisposable
    {
        private readonly ObjectPool<SocketAsyncEventArgs> transmissionArgsPool;

        protected readonly Socket transmitterSocket;

        internal RemoteSocketClient(Socket clientSocket)
        {
            transmitterSocket = clientSocket;

            transmissionArgsPool = new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implementation of dispose pattern.
        /// </summary>
        /// <param name="disposing">
        /// Whether this method is being called by the object finalizer, or by the <see cref="Dispose()"/> method.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                transmitterSocket.Dispose();
            }
        }

        public async ValueTask<TransmissionResult> ReceiveAsync(EndPoint remoteEndPoint, SocketFlags receiveFlags, Memory<byte> receiveBuffer,
            CancellationToken cancellationToken = default)
        {
            SocketAsyncEventArgs transmissionArgs = transmissionArgsPool.Get();

            TransmissionResult receiveResult = await SocketOperations.ReceiveFromAsync(transmissionArgs, transmitterSocket,
                remoteEndPoint, receiveFlags, receiveBuffer, cancellationToken);

            transmissionArgsPool.Return(transmissionArgs);

            return receiveResult;
        }

        public async ValueTask<TransmissionResult> SendAsync(EndPoint remoteEndPoint, SocketFlags sendFlags, Memory<byte> sendBuffer,
            CancellationToken cancellationToken = default)
        {
            SocketAsyncEventArgs transmissionArgs = transmissionArgsPool.Get();

            TransmissionResult sendResult = await SocketOperations.SendToAsync(transmissionArgs, transmitterSocket,
                remoteEndPoint, sendFlags, sendBuffer, cancellationToken);

            transmissionArgsPool.Return(transmissionArgs);

            return sendResult;
        }
    }
}