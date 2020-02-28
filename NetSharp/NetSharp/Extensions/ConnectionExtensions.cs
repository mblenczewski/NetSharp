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
            Memory<byte> inputBuffer, SocketFlags flags)
            => instance.ReceiveAsync(inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static Task<TransmissionResult> ReceiveFromAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> inputBuffer, SocketFlags flags)
            => instance.ReceiveFromAsync(remoteEndPoint, inputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static Task<int> SendAsync(this Connection instance,
            Memory<byte> outputBuffer, SocketFlags flags)
            => instance.SendAsync(outputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static Task<int> SendToAsync(this Connection instance,
            EndPoint remoteEndPoint, Memory<byte> outputBuffer, SocketFlags flags)
            => instance.SendToAsync(remoteEndPoint, outputBuffer, flags, Timeout.InfiniteTimeSpan);

        public static bool TryBind(this Connection instance,
            EndPoint localEndPoint)
            => instance.TryBind(localEndPoint, Timeout.InfiniteTimeSpan);

        public static Task<bool> TryBindAsync(this Connection instance,
            EndPoint localEndPoint)
            => instance.TryBindAsync(localEndPoint, Timeout.InfiniteTimeSpan);
    }
}