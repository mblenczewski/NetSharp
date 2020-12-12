using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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
    public sealed class RawStreamConnection : RawConnectionBase, IRawStreamWriter
    {
        private readonly ConcurrentDictionary<int, RawStreamPacketHandler> registeredHandlers;
        private readonly SlimObjectPool<StateToken> stateTokenPool;

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
            registeredHandlers = new ConcurrentDictionary<int, RawStreamPacketHandler>();

            static StateToken CreateStateToken() => new StateToken();

            static void ResetStateToken(ref StateToken instance) => instance.Reset();

            static void DestroyStateToken(StateToken instance) => instance.Dispose();

            stateTokenPool = new SlimObjectPool<StateToken>(CreateStateToken, ResetStateToken, DestroyStateToken);
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

            StateToken state = stateTokenPool.Rent();
            state.OperationCompletionSource = tcs;

            socketArgs.UserToken = state;

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

            StateToken state = stateTokenPool.Rent();
            state.OperationCompletionSource = tcs;

            socketArgs.UserToken = state;

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
            TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
            SocketAsyncEventArgs socketArgs = RentSocketArgs();

            RawPacketHeader header = new RawPacketHeader(type, buffer.Length);
            byte[] ownedBuffer = RentBuffer(RawPacket.TotalSize(in header));

            StateToken state = stateTokenPool.Rent();

            ConfigureSendRequestAsync(socketArgs, ref ownedBuffer, in header, in buffer, state, tcs);

            socketArgs.SocketFlags = flags;

            StartOrContinueSending(Connection, socketArgs);

            return new ValueTask<int>(tcs.Task);
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
                stateTokenPool.Dispose();
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
            StateToken state,
            TaskCompletionSource<int> tcs)
        {
            Memory<byte> ownedBufferMemory = new Memory<byte>(ownedBuffer);
            RawPacket.Serialise(in ownedBufferMemory, in pendingHeader, in pendingData);

            int totalTransferredBytes = RawPacket.TotalSize(in pendingHeader);
            args.SetBuffer(ownedBuffer, 0, totalTransferredBytes);

            state.BytesToTransfer = totalTransferredBytes;
            state.RequestCompletionSource = tcs;

            args.UserToken = state;
        }

        /// <summary>
        /// Cleans up and returns the given socket args.
        /// </summary>
        private void CleanupArgs(SocketAsyncEventArgs args, bool cleanupUserToken = true)
        {
            if (cleanupUserToken)
            {
                stateTokenPool.Return((StateToken)args.UserToken);
            }

            ReturnSocketArgs(args);
        }

        /// <summary>
        /// Closes the remote network connection associated with the given socket args.
        /// </summary>
        private void CloseClientConnection(SocketAsyncEventArgs args)
        {
            Socket connection = args.AcceptSocket;

            connection.Disconnect(false);
            connection.Shutdown(SocketShutdown.Both);
            connection.Close();
            connection.Dispose();

            CleanupArgs(args);
        }

        /// <summary>
        /// Prepares the given socket args for receiving a packet's data from the network.
        /// </summary>
        private void ConfigureReceiveDataAsync(SocketAsyncEventArgs args, StateToken state, in RawPacketHeader header)
        {
            ReturnBuffer(args.Buffer); // return and clear the previously parsed request header buffer

            byte[] pendingDataBuffer = RentBuffer(header.DataLength);

            args.SetBuffer(pendingDataBuffer, 0, header.DataLength);

            state.BytesToTransfer = header.DataLength;
            state.RequestHeader = header;

            args.UserToken = state;
        }

        /// <summary>
        /// Prepares the given socket args for receiving a packet's header from the network.
        /// </summary>
        private void ConfigureReceiveHeaderAsync(SocketAsyncEventArgs args, StateToken state)
        {
            ReturnBuffer(args.Buffer); // return and clear the previously sent response packet buffer

            byte[] pendingHeaderBuffer = RentBuffer(RawPacketHeader.Length);

            args.SetBuffer(pendingHeaderBuffer, 0, RawPacketHeader.Length);

            state.BytesToTransfer = RawPacketHeader.Length;

            args.UserToken = state;
        }

        /// <summary>
        /// Handles a completed <see cref="Socket.AcceptAsync" /> call.
        /// </summary>
        private void HandleAccepted(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    // the buffer is set to allow a simpler ConfigureReceiveHeader() implementation. Since returning an
                    // empty buffer is ignored in the array pool, this allows us to just return the last assigned buffer
                    // in the ConfigureXXX() method to the pool (this means that usually we will usually be returning
                    // the ResponseDataBuffer).
                    args.SetBuffer(Array.Empty<byte>(), 0, 0);

                    StateToken state = stateTokenPool.Rent();
                    ConfigureReceiveHeaderAsync(args, state);

                    StartOrContinueReceiving(args);
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
            StateToken state = (StateToken)args.UserToken;
            TaskCompletionSource<bool>? tcs = state.OperationCompletionSource;

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
            StateToken state = (StateToken)args.UserToken;
            TaskCompletionSource<bool>? tcs = state.OperationCompletionSource;

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
            StateToken state = (StateToken)args.UserToken;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    switch (state.BytesToTransfer)
                    {
                        case RawPacketHeader.Length:
                            HandleReceivedHeader(args, state);
                            break;

                        default:
                            HandleReceivedData(args, state);
                            break;
                    }

                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.ReceiveAsync" /> call, when receiving a packet's data from the network.
        /// </summary>
        private void HandleReceivedData(SocketAsyncEventArgs args, StateToken state)
        {
            int received = args.BytesTransferred;
            int previouslyReceived = args.Offset;
            int totalReceived = previouslyReceived + received;
            int expected = state.BytesToTransfer;

            RawPacketHeader header = state.RequestHeader!.Value;

            byte[] dataBuffer = args.Buffer;
            ReadOnlyMemory<byte> dataBufferMemory = new ReadOnlyMemory<byte>(dataBuffer, 0, header.DataLength);

            if (totalReceived == expected)
            {
                if (registeredHandlers.TryGetValue(header.Type, out RawStreamPacketHandler handler))
                {
                    handler.Invoke(args.AcceptSocket.RemoteEndPoint, in header, in dataBufferMemory, this);
                }

                ConfigureReceiveHeaderAsync(args, state);
                StartOrContinueReceiving(args);
            }
            else if (totalReceived > 0 && totalReceived < expected)
            {
                args.SetBuffer(totalReceived, expected - totalReceived);
                StartOrContinueReceiving(args);
            }
            else if (received == 0)
            {
                CloseClientConnection(args);
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.ReceiveAsync" /> call, when receiving a packet's header from
        /// the network.
        /// </summary>
        private void HandleReceivedHeader(SocketAsyncEventArgs args, StateToken state)
        {
            int received = args.BytesTransferred;
            int previouslyReceived = args.Offset;
            int totalReceived = previouslyReceived + received;
            int expected = state.BytesToTransfer;

            byte[] headerBuffer = args.Buffer;
            ReadOnlySpan<byte> headerBufferMemory = new ReadOnlySpan<byte>(headerBuffer);

            if (totalReceived == expected)
            {
                RawPacketHeader header = RawPacketHeader.Deserialise(in headerBufferMemory);

                ConfigureReceiveDataAsync(args, state, in header);
                StartOrContinueReceiving(args);
            }
            else if (totalReceived > 0 && totalReceived < expected)
            {
                args.SetBuffer(totalReceived, expected - totalReceived);
                StartOrContinueReceiving(args);
            }
            else if (received == 0)
            {
                CloseClientConnection(args);
            }
        }

        /// <summary>
        /// Handles the completion of a <see cref="Socket.SendAsync" /> call.
        /// </summary>
        private void HandleSent(SocketAsyncEventArgs args)
        {
            StateToken state = (StateToken)args.UserToken;
            TaskCompletionSource<int>? tcs = state.RequestCompletionSource;

            switch (tcs)
            {
                case null:
                    switch (args.SocketError)
                    {
                        case SocketError.Success:
                            HandleSentResponse(args, state);
                            break;

                        default:
                            CloseClientConnection(args);
                            break;
                    }

                    break;

                default:
                    switch (args.SocketError)
                    {
                        case SocketError.Success:
                            HandleSentRequest(args, state);
                            break;

                        case SocketError.OperationAborted:
                            tcs.SetCanceled();
                            CleanupArgs(args);
                            break;

                        default:
                            tcs.SetException(new SocketException((int)args.SocketError));
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
        private void HandleSentRequest(SocketAsyncEventArgs args, StateToken state)
        {
            TaskCompletionSource<int> tcs = state.RequestCompletionSource!;

            int sent = args.BytesTransferred;
            int previouslySent = args.Offset;
            int totalSent = previouslySent + sent;
            int expected = state.BytesToTransfer;

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
        private void HandleSentResponse(SocketAsyncEventArgs args, StateToken state)
        {
            int sent = args.BytesTransferred;
            int previouslySent = args.Offset;
            int totalSent = previouslySent + sent;
            int expected = state.BytesToTransfer;

            if (totalSent == expected)
            {
                ConfigureReceiveHeaderAsync(args, state);
                StartOrContinueReceiving(args);
            }
            else if (totalSent > 0 && totalSent < expected)
            {
                args.SetBuffer(totalSent, expected - totalSent);
                StartOrContinueSending(args.AcceptSocket, args);
            }
            else if (sent == 0)
            {
                CloseClientConnection(args);
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

        /// <summary>
        /// State token for the stream network connection.
        /// </summary>
        private sealed class StateToken : IDisposable
        {
            /// <summary>
            /// The number of bytes that we need to transfer over the network.
            /// </summary>
            internal int BytesToTransfer { get; set; }

            /// <summary>
            /// The <see cref="TaskCompletionSource{TResult}" /> for asynchronous network operations.
            /// </summary>
            internal TaskCompletionSource<bool>? OperationCompletionSource { get; set; }

            /// <summary>
            /// The <see cref="TaskCompletionSource{TResult}" /> for asynchronous packet writes.
            /// </summary>
            internal TaskCompletionSource<int>? RequestCompletionSource { get; set; }

            /// <summary>
            /// The deserialised request packet header.
            /// </summary>
            internal RawPacketHeader? RequestHeader { get; set; }

            public void Dispose()
            {
                Reset();
            }

            internal void Reset()
            {
                BytesToTransfer = 0;
                OperationCompletionSource = null;
                RequestCompletionSource = null;
                RequestHeader = null;
            }
        }
    }
}
