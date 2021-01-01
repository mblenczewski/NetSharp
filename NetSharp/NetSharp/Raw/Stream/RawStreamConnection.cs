using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using NetSharp.Utils;

namespace NetSharp.Raw.Stream
{
    /// <summary>
    /// Handles a message received on a raw stream connection.
    /// </summary>
    /// <param name="remoteEndPoint">
    /// The remote endpoint from which the received message originated.
    /// </param>
    /// <param name="header">
    /// The header of the received message.
    /// </param>
    /// <param name="data">
    /// The data held by the received message.
    /// </param>
    /// <param name="writer">
    /// A reference to the network connection, to interact with the network further.
    /// </param>
    public delegate void RawStreamPacketHandler(
        EndPoint remoteEndPoint,
        in RawPacketHeader header,
        in ReadOnlyMemory<byte> data,
        IRawStreamWriter writer);

    /// <summary>
    /// Represents a network connection using a stream-based protocol to interact over the network, that is capable of
    /// sending raw bytes.
    /// </summary>
    public sealed class RawStreamConnection : RawConnectionBase, IRawStreamWriter, IRawStreamPacketHandler
    {
        private readonly object activeRemoteConnectionsLock = new object();
        private readonly SlimObjectPool<OperationStateToken> operationStatePool;
        private readonly SlimObjectPool<ReaderStateToken> readerStatePool;
        private readonly ConcurrentDictionary<int, RawStreamPacketHandler> registeredHandlers;
        private readonly List<RemoteConnectionWrapper> remoteConnections;
        private readonly object remoteConnectionsLock = new object();
        private readonly SlimObjectPool<WriterStateToken> writerStatePool;

        private int activeRemoteConnections;

        /// <summary>
        /// Initialises a new instance of the <see cref="RawStreamConnection" /> class.
        /// </summary>
        /// <param name="connectionProtocolType">
        /// The protocol that the underlying network connection should use.
        /// </param>
        /// <param name="defaultRemoteEndPoint">
        /// The default remote endpoint that should be used for pending connections.
        /// </param>
        public RawStreamConnection(ProtocolType connectionProtocolType, EndPoint defaultRemoteEndPoint)
            : base(SocketType.Stream, connectionProtocolType, defaultRemoteEndPoint)
        {
            activeRemoteConnections = 0;
            remoteConnections = new List<RemoteConnectionWrapper>();

            registeredHandlers = new ConcurrentDictionary<int, RawStreamPacketHandler>();

            static OperationStateToken CreateOperationToken()
            {
                return new OperationStateToken();
            }

            static void ResetOperationToken(ref OperationStateToken instance)
            {
                instance.Reset();
            }

            static void DestroyOperationToken(OperationStateToken instance)
            {
                instance.Dispose();
            }

            operationStatePool = new SlimObjectPool<OperationStateToken>(
                CreateOperationToken,
                ResetOperationToken,
                DestroyOperationToken);

            static ReaderStateToken CreateReaderToken()
            {
                return new ReaderStateToken();
            }

            static void ResetReaderToken(ref ReaderStateToken instance)
            {
                instance.Reset();
            }

            static void DestroyReaderToken(ReaderStateToken instance)
            {
                instance.Dispose();
            }

            readerStatePool = new SlimObjectPool<ReaderStateToken>(
                CreateReaderToken,
                ResetReaderToken,
                DestroyReaderToken);

            static WriterStateToken CreateWriterToken()
            {
                return new WriterStateToken();
            }

            static void ResetWriterToken(ref WriterStateToken instance)
            {
                instance.Reset();
            }

            static void DestroyWriterToken(WriterStateToken instance)
            {
                instance.Dispose();
            }

            writerStatePool = new SlimObjectPool<WriterStateToken>(
                CreateWriterToken,
                ResetWriterToken,
                DestroyWriterToken);
        }

        /// <summary>
        /// Connects asynchronously to the given remote network endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">
        /// The endpoint of the remote network connection to which we want to connect.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the asynchronous operation.
        /// </returns>
        public Task ConnectAsync(EndPoint remoteEndPoint)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            SocketAsyncEventArgs socketArgs = RentSocketArgs();

            OperationStateToken operationState = operationStatePool.Rent();
            operationState.OperationCompletionSource = tcs;

            socketArgs.UserToken = operationState;

            socketArgs.RemoteEndPoint = remoteEndPoint;

            if (Connection.ConnectAsync(socketArgs))
            {
                return tcs.Task;
            }

            CleanupArgs(socketArgs);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void DeregisterHandler(int id, RawStreamPacketHandler handler)
        {
            if (registeredHandlers.TryGetValue(id, out RawStreamPacketHandler previousMulticast))
            {
                // ReSharper disable once DelegateSubtraction
                RawStreamPacketHandler? newMulticast = previousMulticast - handler;

                if (newMulticast != default)
                {
                    _ = registeredHandlers.TryUpdate(id, newMulticast, previousMulticast);
                }
            }
        }

        /// <summary>
        /// Disconnects asynchronously from the currently connected remote network connection.
        /// </summary>
        /// <param name="leaveConnectionReusable">
        /// Whether the underlying network connection should be left in a reusable state after this call completes.
        /// </param>
        /// <returns>
        /// A <see cref="Task" /> object representing the asynchronous operation.
        /// </returns>
        public Task DisconnectAsync(bool leaveConnectionReusable = false)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            SocketAsyncEventArgs socketArgs = RentSocketArgs();

            OperationStateToken operationState = operationStatePool.Rent();
            operationState.OperationCompletionSource = tcs;

            socketArgs.UserToken = operationState;

            socketArgs.DisconnectReuseSocket = leaveConnectionReusable;

            if (Connection.DisconnectAsync(socketArgs))
            {
                return tcs.Task;
            }

            CleanupArgs(socketArgs);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void RegisterHandler(int id, RawStreamPacketHandler handler)
        {
            _ = registeredHandlers.AddOrUpdate(id, _ => handler, (_, multicast) => multicast + handler);
        }

        /// <inheritdoc />
        public ValueTask<int> SendAsync(ushort type, ReadOnlyMemory<byte> buffer, SocketFlags flags = SocketFlags.None)
        {
            return DoSendAsync(Connection, type, buffer, flags);
        }

        /// <inheritdoc />
        protected override void CreateSocketArgsHook(ref SocketAsyncEventArgs instance)
        {
            if (instance == default)
            {
                return;
            }

            instance.Completed += HandleIoCompleted;

            base.CreateSocketArgsHook(ref instance);
        }

        /// <inheritdoc />
        protected override void DestroySocketArgsHook(ref SocketAsyncEventArgs instance)
        {
            if (instance == default)
            {
                return;
            }

            instance.Completed -= HandleIoCompleted;

            base.DestroySocketArgsHook(ref instance);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (RemoteConnectionWrapper remoteConnection in remoteConnections)
                {
                    remoteConnection.LocalShutdown();
                }

                lock (activeRemoteConnectionsLock)
                {
                    while (activeRemoteConnections > 0)
                    {
                        _ = Monitor.Wait(activeRemoteConnectionsLock);
                    }
                }

                operationStatePool.Dispose();
                readerStatePool.Dispose();
                writerStatePool.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override void HandlerTaskWork()
        {
            while (true)
            {
                SocketAsyncEventArgs socketArgs = RentSocketArgs();

                if (Connection.AcceptAsync(socketArgs))
                {
                    // we want to stop starting accept operations when there are no more connections in the queue. when
                    // this happens Connection.AcceptAsync() will return "true", so we can break.
                    break;
                }

                HandleAccepted(socketArgs);
            }
        }

        /// <inheritdoc />
        protected override void ResetSocketArgsHook(ref SocketAsyncEventArgs instance)
        {
            if (instance == default)
            {
                return;
            }

            instance.AcceptSocket = null;

            base.ResetSocketArgsHook(ref instance);
        }

        /// <inheritdoc />
        protected override void StartHook(int concurrentTasks)
        {
            Connection.Listen(concurrentTasks);

            base.StartHook(concurrentTasks);
        }

        /// <summary>
        /// Prepares the given socket args for sending a request to the network.
        /// </summary>
        private static void ConfigureSendRequestAsync(
            SocketAsyncEventArgs args,
            ref byte[] ownedBuffer,
            in RawPacketHeader pendingHeader,
            in ReadOnlyMemory<byte> pendingData,
            WriterStateToken writerState,
            TaskCompletionSource<int> tcs)
        {
            Memory<byte> ownedBufferMemory = new Memory<byte>(ownedBuffer);
            RawPacket.Serialise(in ownedBufferMemory, in pendingHeader, in pendingData);

            int totalTransferredBytes = RawPacket.TotalSize(in pendingHeader);
            args.SetBuffer(ownedBuffer, 0, totalTransferredBytes);

            writerState.BytesToTransfer = totalTransferredBytes;
            writerState.RequestCompletionSource = tcs;

            args.UserToken = writerState;
        }

        /// <summary>
        /// Cleans up and returns the given socket args.
        /// </summary>
        private void CleanupArgs(SocketAsyncEventArgs args, bool cleanupUserToken = true)
        {
            if (cleanupUserToken)
            {
                switch (args.UserToken)
                {
                    case OperationStateToken operationState:
                        operationStatePool.Return(operationState);
                        break;

                    case ReaderStateToken readerState:
                        readerStatePool.Return(readerState);
                        break;

                    case WriterStateToken writerState:
                        writerStatePool.Return(writerState);
                        break;
                }
            }

            ReturnSocketArgs(args);
        }

        /// <summary>
        /// Prepares the given socket args for receiving a packet's data from the network.
        /// </summary>
        private void ConfigureReceiveDataAsync(SocketAsyncEventArgs args, ReaderStateToken readerState, in RawPacketHeader header)
        {
            ReturnBuffer(args.Buffer); // return and clear the previously parsed request header buffer

            byte[] pendingDataBuffer = RentBuffer(header.DataLength);

            args.SetBuffer(pendingDataBuffer, 0, header.DataLength);

            readerState.BytesToTransfer = header.DataLength;
            readerState.RequestHeader = header;

            args.UserToken = readerState;
        }

        /// <summary>
        /// Prepares the given socket args for receiving a packet's header from the network.
        /// </summary>
        private void ConfigureReceiveHeaderAsync(SocketAsyncEventArgs args, ReaderStateToken readerState)
        {
            ReturnBuffer(args.Buffer); // return and clear the previously sent response packet buffer

            byte[] pendingHeaderBuffer = RentBuffer(RawPacketHeader.Length);

            args.SetBuffer(pendingHeaderBuffer, 0, RawPacketHeader.Length);

            readerState.BytesToTransfer = RawPacketHeader.Length;

            args.UserToken = readerState;
        }

        private ValueTask<int> DoSendAsync(Socket connection, ushort type, ReadOnlyMemory<byte> buffer, SocketFlags flags)
        {
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs socketArgs = RentSocketArgs();

            RawPacketHeader header = new RawPacketHeader(type, buffer.Length);
            byte[] ownedBuffer = RentBuffer(RawPacket.TotalSize(in header));

            WriterStateToken writerState = writerStatePool.Rent();

            ConfigureSendRequestAsync(socketArgs, ref ownedBuffer, in header, in buffer, writerState, tcs);

            socketArgs.SocketFlags = flags;

            StartOrContinueSending(connection, socketArgs);

            return new ValueTask<int>(tcs.Task);
        }

        /// <summary>
        /// Handles a completed <see cref="Socket.AcceptAsync" /> call.
        /// </summary>
        private void HandleAccepted(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    _ = Interlocked.Increment(ref activeRemoteConnections);

                    // the buffer is set to allow a simpler ConfigureReceiveHeader() implementation. Since returning an
                    // empty buffer is ignored in the array pool, this allows us to just return the last assigned buffer
                    // in the ConfigureXXX() method to the pool (this means that usually we will usually be returning
                    // the ResponseDataBuffer).
                    args.SetBuffer(Array.Empty<byte>(), 0, 0);

                    ReaderStateToken readerState = readerStatePool.Rent();

                    Socket remoteConnection = args.AcceptSocket;
                    RemoteConnectionWrapper wrapper = new RemoteConnectionWrapper(this, remoteConnection, readerState);

                    lock (remoteConnectionsLock)
                    {
                        remoteConnections.Add(wrapper);
                    }

                    WaitHandle[] eventHandles =
                    {
                        readerState.ConnectionClosed.WaitHandle,
                        readerState.RequestReceived.WaitHandle,
                    };

                    while (true)
                    {
                        ConfigureReceiveHeaderAsync(args, readerState);
                        StartOrContinueReceiving(args);

                        int completedEvent = WaitHandle.WaitAny(eventHandles);

                        if (completedEvent == 0)
                        {
                            // the connection has been closed
                            break;
                        }

                        // since the request received event has been set, we need to reset it
                        readerState.RequestReceived.Reset();

                        RawPacketHeader header = readerState.RequestHeader!.Value;

                        byte[] dataBuffer = args.Buffer;
                        ReadOnlyMemory<byte> dataBufferMemory = new ReadOnlyMemory<byte>(dataBuffer, 0, header.DataLength);

                        if (registeredHandlers.TryGetValue(header.Type, out RawStreamPacketHandler handler))
                        {
                            handler.Invoke(remoteConnection.RemoteEndPoint, in header, in dataBufferMemory, wrapper);
                        }
                    }

                    lock (activeRemoteConnectionsLock)
                    {
                        _ = Interlocked.Decrement(ref activeRemoteConnections);
                        Monitor.Pulse(activeRemoteConnectionsLock); // signals that we may have reached the activeRemoteConnections == 0 state
                    }

                    lock (remoteConnectionsLock)
                    {
                        // TODO: ensure that this is atomic via locking or something else. are properties inherently atomic???
                        if (!readerState.LocalShutdownSignaled)
                        {
                            // since we were shutdown remotely, we need to remove the remote connection from the list of tracked connections
                            // TODO: come up with a better way of tracking active remote connections (have the wrapper deregister itself?)
                            _ = remoteConnections.Remove(wrapper);

                            remoteConnection.Disconnect(false);
                            remoteConnection.Shutdown(SocketShutdown.Both);
                            remoteConnection.Close();
                            remoteConnection.Dispose();
                        }
                    }

                    CleanupArgs(args);
                    break;

                default:
                    CleanupArgs(args, false); // there is no StateToken to cleanup
                    break;
            }
        }

        /// <summary>
        /// Handles a completed <see cref="Socket.ConnectAsync(SocketAsyncEventArgs)" /> call.
        /// </summary>
        private void HandleConnected(SocketAsyncEventArgs args)
        {
            OperationStateToken readerState = (OperationStateToken)args.UserToken;
            TaskCompletionSource<bool>? tcs = readerState.OperationCompletionSource;

            Debug.Assert(
                tcs != default,
                "HandleConnected was passed a state token without the correct TaskCompletionSource!");

            switch (args.SocketError)
            {
                case SocketError.Success:
                    tcs.SetResult(true);
                    break;

                case SocketError.OperationAborted:
                    tcs.SetCanceled();
                    break;

                default:
                    tcs.SetException(new SocketException((int)args.SocketError));
                    break;
            }

            CleanupArgs(args);
        }

        /// <summary>
        /// Handles a completed <see cref="Socket.DisconnectAsync" /> call.
        /// </summary>
        private void HandleDisconnected(SocketAsyncEventArgs args)
        {
            OperationStateToken readerState = (OperationStateToken)args.UserToken;
            TaskCompletionSource<bool>? tcs = readerState.OperationCompletionSource;

            Debug.Assert(
                tcs != default,
                "HandleDisconnected was passed a state token without the correct TaskCompletionSource!");

            switch (args.SocketError)
            {
                case SocketError.Success:
                    tcs.SetResult(true);
                    break;

                case SocketError.OperationAborted:
                    tcs.SetCanceled();
                    break;

                default:
                    tcs.SetException(new SocketException((int)args.SocketError));
                    break;
            }

            CleanupArgs(args);
        }

        /// <summary>
        /// Handles the completion of an asynchronous socket operation.
        /// </summary>
        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    if (args.SocketError != SocketError.OperationAborted)
                    {
                        HandlerTaskWork();
                    }

                    HandleAccepted(args);
                    break;

                case SocketAsyncOperation.Connect:
                    HandleConnected(args);
                    break;

                case SocketAsyncOperation.Disconnect:
                    HandleDisconnected(args);
                    break;

                case SocketAsyncOperation.Receive:
                    HandleReceived(args);
                    break;

                case SocketAsyncOperation.Send:
                    HandleSent(args);
                    break;
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.ReceiveAsync" /> call.
        /// </summary>
        private void HandleReceived(SocketAsyncEventArgs args)
        {
            ReaderStateToken readerState = (ReaderStateToken)args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    switch (readerState.BytesToTransfer)
                    {
                        case RawPacketHeader.Length:
                            HandleReceivedHeader(args, readerState);
                            break;

                        default:
                            HandleReceivedData(args, readerState);
                            break;
                    }

                    break;

                default:
                    readerState.ConnectionClosed.Set();
                    break;
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.ReceiveAsync" /> call, when receiving a packet's data from the network.
        /// </summary>
        private void HandleReceivedData(SocketAsyncEventArgs args, ReaderStateToken readerState)
        {
            int received = args.BytesTransferred;
            int previouslyReceived = args.Offset;
            int totalReceived = previouslyReceived + received;
            int expected = readerState.BytesToTransfer;

            if (totalReceived == expected)
            {
                readerState.RequestReceived.Set();
            }
            else if (totalReceived > 0 && totalReceived < expected)
            {
                args.SetBuffer(totalReceived, expected - totalReceived);
                StartOrContinueReceiving(args);
            }
            else if (received == 0)
            {
                readerState.ConnectionClosed.Set();
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.ReceiveAsync" /> call, when receiving a packet's header from
        /// the network.
        /// </summary>
        private void HandleReceivedHeader(SocketAsyncEventArgs args, ReaderStateToken readerState)
        {
            int received = args.BytesTransferred;
            int previouslyReceived = args.Offset;
            int totalReceived = previouslyReceived + received;
            int expected = readerState.BytesToTransfer;

            byte[] headerBuffer = args.Buffer;
            ReadOnlySpan<byte> headerBufferMemory = new ReadOnlySpan<byte>(headerBuffer);

            if (totalReceived == expected)
            {
                RawPacketHeader header = RawPacketHeader.Deserialise(in headerBufferMemory);

                ConfigureReceiveDataAsync(args, readerState, in header);
                StartOrContinueReceiving(args);
            }
            else if (totalReceived > 0 && totalReceived < expected)
            {
                args.SetBuffer(totalReceived, expected - totalReceived);
                StartOrContinueReceiving(args);
            }
            else if (received == 0)
            {
                readerState.ConnectionClosed.Set();
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.SendAsync" /> call.
        /// </summary>
        private void HandleSent(SocketAsyncEventArgs args)
        {
            switch (args.UserToken)
            {
                case ReaderStateToken readerState:
                    switch (args.SocketError)
                    {
                        case SocketError.Success:
                            HandleSentResponse(args, readerState);
                            break;

                        default:
                            readerState.ConnectionClosed.Set();
                            break;
                    }

                    break;

                case WriterStateToken writerState:
                    switch (args.SocketError)
                    {
                        case SocketError.Success:
                            HandleSentRequest(args, writerState);
                            break;

                        case SocketError.OperationAborted:
                            writerState.RequestCompletionSource!.SetCanceled();
                            CleanupArgs(args);
                            break;

                        default:
                            writerState.RequestCompletionSource!.SetException(
                                new SocketException((int)args.SocketError));
                            CleanupArgs(args);
                            break;
                    }

                    break;
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.SendAsync" /> call, when sending a request packet to the
        /// network. In this case, the <see cref="SocketAsyncEventArgs.ConnectSocket" /> will be used to perform the transmission.
        /// </summary>
        private void HandleSentRequest(SocketAsyncEventArgs args, WriterStateToken writerState)
        {
            TaskCompletionSource<int> tcs = writerState.RequestCompletionSource!;

            int sent = args.BytesTransferred;
            int previouslySent = args.Offset;
            int totalSent = previouslySent + sent;
            int expected = writerState.BytesToTransfer;

            if (totalSent == expected)
            {
                tcs.SetResult(totalSent - RawPacketHeader.Length);
                CleanupArgs(args);
            }
            else if (totalSent > 0 && totalSent < expected)
            {
                args.SetBuffer(totalSent, expected - totalSent);
                StartOrContinueSending(args.ConnectSocket, args);
            }
            else if (sent == 0)
            {
                // connection is dead
                tcs.SetException(new SocketException((int)SocketError.HostDown));
                CleanupArgs(args);
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.SendAsync" /> call, when sending a response packet to the
        /// network. In this case, the <see cref="SocketAsyncEventArgs.AcceptSocket" /> will be used to perform the transmission.
        /// </summary>
        private void HandleSentResponse(SocketAsyncEventArgs args, ReaderStateToken readerState)
        {
            int sent = args.BytesTransferred;
            int previouslySent = args.Offset;
            int totalSent = previouslySent + sent;
            int expected = readerState.BytesToTransfer;

            if (totalSent == expected)
            {
                // TODO do we need to do any notification that a send has completed?
            }
            else if (totalSent > 0 && totalSent < expected)
            {
                args.SetBuffer(totalSent, expected - totalSent);
                StartOrContinueSending(args.AcceptSocket, args);
            }
            else if (sent == 0)
            {
                readerState.ConnectionClosed.Set();
            }
        }

        /// <summary>
        /// Starts or continues an asynchronous network read operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StartOrContinueReceiving(SocketAsyncEventArgs args)
        {
            if (args.AcceptSocket.ReceiveAsync(args))
            {
                return;
            }

            HandleReceived(args);
        }

        /// <summary>
        /// Starts or continues an asynchronous network write operation using the given socket.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StartOrContinueSending(Socket connection, SocketAsyncEventArgs args)
        {
            if (connection.SendAsync(args))
            {
                return;
            }

            HandleSent(args);
        }

        private sealed class OperationStateToken : IDisposable
        {
            /// <summary>
            /// The <see cref="TaskCompletionSource{TResult}" /> for asynchronous network operations.
            /// </summary>
            internal TaskCompletionSource<bool>? OperationCompletionSource { get; set; }

            /// <inheritdoc />
            public void Dispose()
            {
                Reset();
            }

            internal void Reset()
            {
                OperationCompletionSource = null;
            }
        }

        private sealed class ReaderStateToken : IDisposable
        {
            internal int BytesToTransfer { get; set; }

            internal ManualResetEventSlim ConnectionClosed { get; } = new ManualResetEventSlim(false);

            internal bool LocalShutdownSignaled { get; set; }

            internal RawPacketHeader? RequestHeader { get; set; }

            internal ManualResetEventSlim RequestReceived { get; } = new ManualResetEventSlim(false);

            /// <inheritdoc />
            public void Dispose()
            {
                Reset();

                ConnectionClosed.Dispose();
                RequestReceived.Dispose();
            }

            internal void Reset()
            {
                BytesToTransfer = 0;
                RequestHeader = null;

                ConnectionClosed.Reset();
                RequestReceived.Reset();
            }
        }

        private sealed class RemoteConnectionWrapper : IRawStreamWriter
        {
            private readonly WeakReference<Socket> connectionRef;
            private readonly WeakReference<RawStreamConnection> parentRef;
            private readonly WeakReference<ReaderStateToken> tokenRef;

            internal RemoteConnectionWrapper(RawStreamConnection parent, Socket connection, ReaderStateToken token)
            {
                parentRef = new WeakReference<RawStreamConnection>(parent);
                connectionRef = new WeakReference<Socket>(connection);
                tokenRef = new WeakReference<ReaderStateToken>(token);
            }

            public void LocalShutdown()
            {
                bool connectionDisposed = !connectionRef.TryGetTarget(out Socket connection);
                bool tokenDisposed = !tokenRef.TryGetTarget(out ReaderStateToken token);

                if (connectionDisposed || tokenDisposed)
                {
                    return;
                }

                token.LocalShutdownSignaled = true;

                connection.Disconnect(false);
                connection.Shutdown(SocketShutdown.Both);
                connection.Close();
                connection.Dispose();
            }

            /// <inheritdoc />
            public ValueTask<int> SendAsync(ushort type, ReadOnlyMemory<byte> buffer, SocketFlags flags = SocketFlags.None)
            {
                bool parentDisposed = !parentRef.TryGetTarget(out RawStreamConnection parent);
                bool connectionDisposed = !connectionRef.TryGetTarget(out Socket connection);

                if (parentDisposed || connectionDisposed)
                {
                    // TODO: just use a result of -1? some other error code?
                    throw new ObjectDisposedException(parentDisposed ? nameof(parent) : nameof(connection));
                }

                return parent.DoSendAsync(connection, type, buffer, flags);
            }
        }

        private sealed class WriterStateToken : IDisposable
        {
            internal int BytesToTransfer { get; set; }

            internal TaskCompletionSource<int>? RequestCompletionSource { get; set; }

            /// <inheritdoc />
            public void Dispose()
            {
                Reset();
            }

            internal void Reset()
            {
                BytesToTransfer = 0;
                RequestCompletionSource = null;
            }
        }
    }
}
