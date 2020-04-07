using NetSharp.Utils;

using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets
{
    internal readonly struct AsyncAcceptToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<bool> CompletionSource;

        public AsyncAcceptToken(in TaskCompletionSource<bool> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }

    internal readonly struct AsyncConnectToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<bool> CompletionSource;

        public AsyncConnectToken(in TaskCompletionSource<bool> tcs, in CancellationToken cancellationToken = default)
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

    internal readonly struct AsyncWriteToToken
    {
        public readonly CancellationToken CancellationToken;
        public readonly TaskCompletionSource<TransmissionResult> CompletionSource;

        public AsyncWriteToToken(in TaskCompletionSource<TransmissionResult> tcs, in CancellationToken cancellationToken = default)
        {
            CompletionSource = tcs;
            CancellationToken = cancellationToken;
        }
    }
}