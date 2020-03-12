using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Utils;

namespace NetSharp.Extensions
{
    /// <summary>
    /// Provides additional methods and functionality to the <see cref="Connection"/> class.
    /// </summary>
    public static class ConnectionExtensions
    {
        public static Task<TransmissionResult> ReceiveAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags)
            => instance.ReceiveAsync(inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static Task<TransmissionResult> ReceiveFromAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags)
            => instance.ReceiveFromAsync(remoteEndPoint, inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static ValueTask<int> SendAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags)
            => instance.SendAsync(outputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static ValueTask<int> SendToAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags)
            => instance.SendToAsync(remoteEndPoint, outputBuffer, flags, Timeout.InfiniteTimeSpan);

        /// <summary>
        /// Attempts to synchronously bind the underlying socket to the given local endpoint. Blocks.
        /// If the timeout is exceeded the binding attempt is aborted and the method returns false.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="timeout">The timeout within which to attempt the binding.</param>
        /// <returns>Whether the binding was successful or not.</returns>
        public static bool TryBind(this Connection instance,
            EndPoint localEndPoint, TimeSpan timeout)
            => instance.TryBindAsync(localEndPoint, timeout).Result;

        public static bool TryBind(this Connection instance,
            EndPoint localEndPoint)
            => instance.TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan).Result;

        public static Task<bool> TryBindAsync(this Connection instance,
            EndPoint localEndPoint)
            => instance.TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan);

        public static bool TryConnect(this Connection instance,
            EndPoint remoteEndPoint)
            => instance.TryConnectAsync(remoteEndPoint, Timeout.InfiniteTimeSpan).Result;

        public static bool TryConnect(this Connection instance,
            EndPoint remoteEndPoint, TimeSpan timeout)
            => instance.TryConnectAsync(remoteEndPoint, timeout).Result;

        public static Task<bool> TryConnectAsync(this Connection instance,
            EndPoint remoteEndPoint)
            => instance.TryConnectAsync(remoteEndPoint, Timeout.InfiniteTimeSpan);

        public static bool TryDisconnect(this Connection instance)
            => instance.TryDisconnectAsync(Timeout.InfiniteTimeSpan).Result;

        public static bool TryDisconnect(this Connection instance,
            TimeSpan timeout)
            => instance.TryDisconnectAsync(timeout).Result;

        public static Task<bool> TryDisconnectAsync(this Connection instance)
            => instance.TryDisconnectAsync(Timeout.InfiniteTimeSpan);
    }
}