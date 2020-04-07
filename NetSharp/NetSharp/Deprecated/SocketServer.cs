using Microsoft.Extensions.ObjectPool;

using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
{
    public class SocketServer : IDisposable
    {
        private readonly ObjectPool<SocketAsyncEventArgs> transmissionArgsPool;

        /// <summary>
        /// Destroys a socket server instance.
        /// </summary>
        ~SocketServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// The socket which should be used to listen for incoming data and to send outgoing data.
        /// </summary>
        protected readonly Socket listenerSocket;

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
                listenerSocket.Dispose();
            }
        }

        public SocketServer(AddressFamily listenerAddressFamily, SocketType listenerSocketType,
                    ProtocolType listenerProtocolType)
        {
            listenerSocket = new Socket(listenerAddressFamily, listenerSocketType, listenerProtocolType);

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
                await SocketOperations.ReceiveFromAsync(transmissionArgs, listenerSocket, remoteEndPoint, receiveFlags,
                    receiveBuffer, cancellationToken).ConfigureAwait(false);

            transmissionArgsPool.Return(transmissionArgs);

            return receiveResult;
        }

        public async ValueTask<TransmissionResult> SendAsync(EndPoint remoteEndPoint, SocketFlags sendFlags, Memory<byte> sendBuffer,
            CancellationToken cancellationToken = default)
        {
            SocketAsyncEventArgs transmissionArgs = transmissionArgsPool.Get();

            TransmissionResult sendResult =
                await SocketOperations.SendToAsync(transmissionArgs, listenerSocket, remoteEndPoint, sendFlags,
                    sendBuffer, cancellationToken).ConfigureAwait(false);

            transmissionArgsPool.Return(transmissionArgs);

            return sendResult;
        }

        public async Task<RemoteSocketClient> AcceptAsync(CancellationToken cancellationToken = default)
        {
            using SocketAsyncEventArgs clientAcceptArgs = new SocketAsyncEventArgs();

            Socket clientSocket = await SocketOperations
                .AcceptAsync(clientAcceptArgs, listenerSocket, cancellationToken).ConfigureAwait(false);

            return new RemoteSocketClient(clientSocket);
        }

        public Task<bool> TryBindAsync(EndPoint localEndPoint, TimeSpan timeout)
        {
            using CancellationTokenSource cts = new CancellationTokenSource(timeout);

            try
            {
                return Task.Run(() =>
                {
                    listenerSocket.Bind(localEndPoint);

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