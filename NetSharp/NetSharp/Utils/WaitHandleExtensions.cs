using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Utils
{
    public static class WaitHandleExtensions
    {
        public static Task<bool> WaitOneAsync(this WaitHandle instance, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            RegisteredWaitHandle? registeredHandle = default;
            CancellationTokenRegistration tokenRegistration = default;

            try
            {
                registeredHandle = ThreadPool.RegisterWaitForSingleObject(
                    instance,
                    (state, timedOut) => ((TaskCompletionSource<bool>)state).TrySetResult(!timedOut),
                    tcs,
                    timeout,
                    true);

                tokenRegistration = cancellationToken.Register(
                    state => ((TaskCompletionSource<bool>)state).TrySetCanceled(),
                    tcs);

                return tcs.Task;
            }
            finally
            {
                registeredHandle?.Unregister(null);
                tokenRegistration.Dispose();
            }
        }

        public static Task<bool> WaitOneAsync(this WaitHandle instance, int timeoutMs, CancellationToken cancellationToken = default)
            => instance.WaitOneAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);

        public static Task<bool> WaitOneAsync(this WaitHandle instance, CancellationToken cancellationToken = default)
            => instance.WaitOneAsync(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}