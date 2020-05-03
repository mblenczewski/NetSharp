using NetSharp.Packets;

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Stream
{
    /// <summary>
    /// Provides additional configuration options for a <see cref="StreamSocketServer" /> instance.
    /// </summary>
    public readonly struct StreamSocketServerOptions
    {
        /// <summary>
        /// The default configuration.
        /// </summary>
        public static readonly StreamSocketServerOptions Defaults =
            new StreamSocketServerOptions(Environment.ProcessorCount, 0);

        /// <summary>
        /// The number of <see cref="Socket.AcceptAsync" /> calls that will be 'in-flight' at any one time, and ready to service incoming client
        /// connection requests. This should be set according to the number of client which will be attempting to connect at once.
        /// </summary>
        public readonly int ConcurrentAcceptCalls;

        /// <summary>
        /// The number of <see cref="SocketAsyncEventArgs" /> instances that should be preallocated for use in the <see cref="Socket.SendAsync" /> and
        /// <see cref="Socket.ReceiveAsync" /> methods.
        /// </summary>
        public readonly ushort PreallocatedTransmissionArgs;

        /// <summary>
        /// Constructs a new instance of the <see cref="StreamSocketServerOptions" /> struct.
        /// </summary>
        /// <param name="concurrentAcceptCalls">
        /// The number of <see cref="Socket.AcceptAsync" /> calls which should be 'in-flight' at any one time.
        /// </param>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of <see cref="SocketAsyncEventArgs" /> instances to preallocate.
        /// </param>
        public StreamSocketServerOptions(int concurrentAcceptCalls, ushort preallocatedTransmissionArgs)
        {
            ConcurrentAcceptCalls = concurrentAcceptCalls;

            PreallocatedTransmissionArgs = preallocatedTransmissionArgs;
        }
    }

    //TODO address the need to handle series of network packets, not just single packets
    //TODO document class
    public sealed class StreamSocketServer : RawSocketServer
    {
        private readonly StreamSocketServerOptions serverOptions;

        /// <summary>
        /// Constructs a new instance of the <see cref="StreamSocketServer" /> class.
        /// </summary>
        /// <param name="serverOptions">
        /// Additional options to configure the server.
        /// </param>
        /// <inheritdoc />
        public StreamSocketServer(ref Socket rawConnection, in RawRequestPacketHandler packetHandler, in StreamSocketServerOptions? serverOptions = null)
            : base(ref rawConnection,
                NetworkPacket.TotalSize,
                serverOptions?.PreallocatedTransmissionArgs ?? StreamSocketServerOptions.Defaults.PreallocatedTransmissionArgs,
                packetHandler)
        {
            if (rawConnection.SocketType != SocketType.Stream)
            {
                throw new ArgumentException($"Only {SocketType.Stream} is supported!", nameof(rawConnection));
            }

            this.serverOptions = serverOptions ?? StreamSocketServerOptions.Defaults;
        }

        public ref readonly StreamSocketServerOptions ServerOptions
        {
            get { return ref serverOptions; }
        }

        private void Accept(SocketAsyncEventArgs acceptArgs)
        {
            bool operationPending = Connection.AcceptAsync(acceptArgs);

            if (operationPending) return;

            SocketAsyncEventArgs newAcceptArgs = ArgsPool.Rent();

            Accept(newAcceptArgs); // start a new accept operation to not miss any clients

            CompleteAccept(acceptArgs);
        }

        private void CloseClientSocket(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;
            clientToken.Dispose();

            ArgsPool.Return(clientArgs);
        }

        private void CompleteAccept(SocketAsyncEventArgs connectedClientArgs)
        {
            Socket clientSocket = connectedClientArgs.AcceptSocket;

            RemoteStreamClientToken clientToken = new RemoteStreamClientToken(in clientSocket);

            connectedClientArgs.UserToken = clientToken;

            Receive(connectedClientArgs);
        }

        private void CompleteReceive(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken receiveToken = (RemoteStreamClientToken)clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == NetworkPacket.TotalSize)
                {
                    // buffer was fully received
                    byte[] responseBuffer = BufferPool.Rent(MaxBufferSize);

                    bool responseExists = PacketHandler(clientArgs.RemoteEndPoint, receiveToken.RentedBuffer, responseBuffer);
                    BufferPool.Return(receiveToken.RentedBuffer, true); // at this point the request buffer can be returned

                    //NetworkPacket.Deserialise(receiveToken.RentedBuffer, out NetworkPacket request);
                    //NetworkPacket response = PacketHandler(in request, clientArgs.RemoteEndPoint);

                    if (responseExists)
                    {
                        //NetworkPacket.Serialise(response, responseBufferMemory);

                        receiveToken.RentedBuffer = responseBuffer;
                        clientArgs.SetBuffer(responseBuffer, 0, NetworkPacket.TotalSize);

                        Send(clientArgs);
                    }
                    else
                    {
                        BufferPool.Return(responseBuffer, true);
                    }

                    Receive(clientArgs);
                }
                else if (NetworkPacket.TotalSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // receive the remaining parts of the buffer

                    int receivedBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(receivedBytes, NetworkPacket.TotalSize - receivedBytes);

                    Receive(clientArgs);
                }
                else
                {
                    // no bytes were received, remote socket is dead

                    CloseClientSocket(clientArgs);
                }
            }
            else
            {
                CloseClientSocket(clientArgs);
            }
        }

        private void CompleteSend(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken sendToken = (RemoteStreamClientToken)clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == NetworkPacket.TotalSize)
                {
                    // buffer was fully sent

                    BufferPool.Return(sendToken.RentedBuffer, true);

                    sendToken.RentedBuffer = null;

                    Receive(clientArgs);
                }
                else if (NetworkPacket.TotalSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // send the remaining parts of the buffer

                    int sentBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(sentBytes, NetworkPacket.TotalSize - sentBytes);

                    Send(clientArgs);
                }
                else
                {
                    // no bytes were sent, remote socket is dead

                    CloseClientSocket(clientArgs);
                }
            }
            else
            {
                CloseClientSocket(clientArgs);
            }
        }

        private void Receive(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;

            byte[] requestBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            clientToken.RentedBuffer = requestBuffer;
            clientArgs.SetBuffer(clientToken.RentedBuffer, 0, NetworkPacket.TotalSize);

            bool operationPending = clientToken.ClientSocket.ReceiveAsync(clientArgs);

            if (!operationPending)
            {
                CompleteReceive(clientArgs);
            }
        }

        private void Send(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;

            bool operationPending = clientToken.ClientSocket.SendAsync(clientArgs);

            if (!operationPending)
            {
                CompleteSend(clientArgs);
            }
        }

        /// <inheritdoc />
        protected override bool CanTransmissionArgsBeReused(ref SocketAsyncEventArgs args)
        {
            return true;
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs();

            connectionArgs.Completed += HandleIoCompleted;

            return connectionArgs;
        }

        /// <inheritdoc />
        protected override void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.Completed -= HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        /// <inheritdoc />
        protected override void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    SocketAsyncEventArgs newAcceptArgs = ArgsPool.Rent();

                    Accept(newAcceptArgs); // start a new accept operation to not miss any clients

                    CompleteAccept(args);

                    break;

                case SocketAsyncOperation.Receive:
                    CompleteReceive(args);

                    break;

                case SocketAsyncOperation.Send:
                    CompleteSend(args);

                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        /// <inheritdoc />
        protected override void ResetTransmissionArgs(ref SocketAsyncEventArgs args)
        {
        }

        /// <inheritdoc />
        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            Connection.Listen(100);

            for (int i = 0; i < serverOptions.ConcurrentAcceptCalls; i++)
            {
                SocketAsyncEventArgs acceptArgs = ArgsPool.Rent();

                Accept(acceptArgs);
            }

            cancellationToken.WaitHandle.WaitOne();

            return Task.CompletedTask;
        }

        private class RemoteStreamClientToken : IDisposable
        {
            public readonly Socket ClientSocket;

            public byte[]? RentedBuffer;

            public RemoteStreamClientToken(in Socket clientSocket)
            {
                ClientSocket = clientSocket;
            }

            public void Dispose()
            {
                ClientSocket.Shutdown(SocketShutdown.Both);
                ClientSocket.Close();
                ClientSocket.Dispose();
            }
        }
    }
}