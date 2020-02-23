using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Extensions
{
    /// <summary>
    /// Provides additional methods and functionality to the <see cref="Socket"/> class.
    /// </summary>
    public static class SocketExtensions
    {
        /// <inheritdoc cref="Socket.ReceiveAsync"/>
        public static SocketTask ReceiveAsync(this Socket instance, SocketTask awaitableTask)
        {
            awaitableTask.Reset();
            if (!instance.ReceiveAsync(awaitableTask.eventArgs))
            {
                awaitableTask.wasCompleted = true;
            }

            return awaitableTask;
        }

        /// <inheritdoc cref="Socket.ReceiveFromAsync"/>
        public static SocketTask ReceiveFromAsync(this Socket instance, SocketTask awaitableTask)
        {
            awaitableTask.Reset();
            if (!instance.ReceiveFromAsync(awaitableTask.eventArgs))
            {
                awaitableTask.wasCompleted = true;
            }

            return awaitableTask;
        }

        /// <inheritdoc cref="Socket.ReceiveMessageFromAsync"/>
        public static SocketTask ReceiveMessageFromAsync(this Socket instance, SocketTask awaitableTask)
        {
            awaitableTask.Reset();
            if (!instance.ReceiveMessageFromAsync(awaitableTask.eventArgs))
            {
                awaitableTask.wasCompleted = true;
            }

            return awaitableTask;
        }

        /// <inheritdoc cref="Socket.SendAsync"/>
        public static SocketTask SendAsync(this Socket instance, SocketTask awaitableTask)
        {
            awaitableTask.Reset();
            if (!instance.SendAsync(awaitableTask.eventArgs))
            {
                awaitableTask.wasCompleted = true;
            }

            return awaitableTask;
        }

        /// <inheritdoc cref="Socket.SendToAsync"/>
        public static SocketTask SendToAsync(this Socket instance, SocketTask awaitableTask)
        {
            awaitableTask.Reset();
            if (!instance.SendToAsync(awaitableTask.eventArgs))
            {
                awaitableTask.wasCompleted = true;
            }

            return awaitableTask;
        }
    }

    /// <summary>
    /// Custom awaitable to ease the use of sockets with the TAP pattern.
    /// Credit goes to https://devblogs.microsoft.com/pfxteam/awaiting-socket-operations/.
    /// </summary>
    public sealed class SocketTask : INotifyCompletion
    {
        /// <summary>
        /// Representing a null action.
        /// </summary>
        private static readonly Action SentinelAction = () => { };

        /// <summary>
        /// The action that should be invoked upon the completion of the socket task.
        /// </summary>
        internal Action? continuationAction;

        /// <summary>
        /// The underlying socket event args for this socket task.
        /// </summary>
        internal SocketAsyncEventArgs eventArgs;

        /// <summary>
        /// Whether this socket task was completed.
        /// </summary>
        internal bool wasCompleted;

        /// <summary>
        /// Resets this socket task to its default state, and sets <see cref="continuationAction"/> to <c>default</c>.
        /// </summary>
        internal void Reset()
        {
            wasCompleted = false;
            continuationAction = default;
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="SocketTask"/> class.
        /// </summary>
        /// <param name="asyncEventArgs">The socket event args that this socket task should wrap. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown if the given <paramref name="asyncEventArgs"/> were <c>null</c>.</exception>
        public SocketTask(SocketAsyncEventArgs? asyncEventArgs)
        {
            eventArgs = asyncEventArgs ??
                        throw new ArgumentNullException(nameof(asyncEventArgs), "The given asynchronous socket event args were null.");

            eventArgs.Completed += delegate
            {
                Action? previousAction = continuationAction ??
                                     Interlocked.CompareExchange(ref continuationAction, SentinelAction, default);

                previousAction?.Invoke();
            };
        }

        /// <summary>
        /// Whether this socket task has been completed.
        /// </summary>
        public bool IsCompleted
        {
            get { return wasCompleted; }
        }

        /// <summary>
        /// Returns this socket task instance.
        /// </summary>
        public SocketTask GetAwaiter()
        {
            return this;
        }

        /// <summary>
        /// Throws a <see cref="SocketException"/> if the wrapped <see cref="SocketAsyncEventArgs.SocketError"/>
        /// is not equal to <see cref="SocketError.Success"/>.
        /// </summary>
        /// <exception cref="SocketException">
        /// Thrown if the wrapped <see cref="SocketAsyncEventArgs"/> did not complete successfully.
        /// </exception>
        public void GetResult()
        {
            if (eventArgs.SocketError != SocketError.Success)
            {
                throw new SocketException((int)eventArgs.SocketError);
            }
        }

        /// <inheritdoc />
        public void OnCompleted(Action? continuation)
        {
            if (continuationAction == SentinelAction ||
                Interlocked.CompareExchange(ref continuationAction, continuation, default) == SentinelAction)
            {
                Task.Run(continuation);
            }
        }
    }
}