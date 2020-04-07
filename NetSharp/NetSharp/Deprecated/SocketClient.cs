using Microsoft.Extensions.ObjectPool;

using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
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

        public async ValueTask<TransmissionResult> ReceiveAsync(EndPoint remoteEndPoint, SocketFlags receiveFlags, Memory<byte> receiveBuffer,
            CancellationToken cancellationToken = default)
        {
            SocketAsyncEventArgs transmissionArgs = transmissionArgsPool.Get();

            TransmissionResult receiveResult =
                await SocketOperations.ReceiveFromAsync(transmissionArgs, transmitterSocket,
                remoteEndPoint, receiveFlags, receiveBuffer, cancellationToken).ConfigureAwait(false);

            transmissionArgsPool.Return(transmissionArgs);

            return receiveResult;
        }

        public async ValueTask<TransmissionResult> SendAsync(EndPoint remoteEndPoint, SocketFlags sendFlags, Memory<byte> sendBuffer,
            CancellationToken cancellationToken = default)
        {
            SocketAsyncEventArgs transmissionArgs = transmissionArgsPool.Get();

            TransmissionResult sendResult =
                await SocketOperations.SendToAsync(transmissionArgs, transmitterSocket,
                remoteEndPoint, sendFlags, sendBuffer, cancellationToken).ConfigureAwait(false);

            transmissionArgsPool.Return(transmissionArgs);

            return sendResult;
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

        public Task<bool> TryConnectAsync(EndPoint remoteEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);

            try
            {
                return Task.Run(() =>
                {
                    transmitterSocket.Connect(remoteEndPoint);

                    return true;
                }, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return Task.FromResult(false);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket exception on connecting to {remoteEndPoint}: {ex}");
                return Task.FromResult(false);
            }
        }
    }
}