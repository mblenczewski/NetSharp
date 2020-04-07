using NetSharp.Utils;

using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Deprecated
{
    internal readonly struct AsyncAcceptToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<Socket> CompletionSource;

        public AsyncAcceptToken(in TaskCompletionSource<Socket> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncConnectToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<Socket> CompletionSource;

        public AsyncConnectToken(in TaskCompletionSource<Socket> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncDisconnectToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<bool> CompletionSource;

        public AsyncDisconnectToken(in TaskCompletionSource<bool> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncReadToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

        public AsyncReadToken(in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncWriteToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

        public AsyncWriteToken(in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncReadFromToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

        public AsyncReadFromToken(in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }
}