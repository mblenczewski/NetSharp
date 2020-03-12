using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Utils;

namespace NetSharp.Sockets
{
    public class SocketClient : IDisposable
    {
        private readonly ObjectPool<SocketAsyncEventArgs> transmissionArgsPool;

        /// <summary>
        /// Destroys a socket client instance.
        /// </summary>
        ~SocketClient()
        {
            Dispose(false);
        }

        protected readonly Socket transmitterSocket;

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

        public SocketClient(AddressFamily transmitterAddressFamily, SocketType transmitterSocketType,
            ProtocolType transmitterProtocolType)
        {
            transmitterSocket = new Socket(transmitterAddressFamily, transmitterSocketType, transmitterProtocolType);

            transmissionArgsPool = new DefaultObjectPool<SocketAsyncEventArgs>(new DefaultPooledObjectPolicy<SocketAsyncEventArgs>());
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ValueTask<TransmissionResult> ReceiveAsync(EndPoint remoteEndPoint, SocketFlags receiveFlags, Memory<byte> receiveBuffer,
            CancellationToken cancellationToken = default)
        {
            return SocketOperations.ReceiveFromAsync(transmissionArgsPool, transmitterSocket, remoteEndPoint,
                receiveFlags, receiveBuffer, cancellationToken);
        }

        public ValueTask<TransmissionResult> SendAsync(EndPoint remoteEndPoint, SocketFlags sendFlags, Memory<byte> sendBuffer,
            CancellationToken cancellationToken = default)
        {
            return SocketOperations.SendToAsync(transmissionArgsPool, transmitterSocket, remoteEndPoint, sendFlags,
                sendBuffer, cancellationToken);
        }

        public Task<bool> TryBindAsync(EndPoint localEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);

            try
            {
                return Task.Run(() =>
                {
                    transmitterSocket.Bind(localEndPoint);

                    return true;
                }, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return Task.FromResult(false);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket exception on binding socket to {localEndPoint}: {ex}");
                return Task.FromResult(false);
            }
        }
    }
}