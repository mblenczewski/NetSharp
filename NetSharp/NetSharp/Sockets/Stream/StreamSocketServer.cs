using NetSharp.Packets;

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Stream
{
    //TODO document
    public readonly struct StreamSocketServerOptions
    {
        public static readonly StreamSocketServerOptions Defaults =
            new StreamSocketServerOptions(NetworkPacket.TotalSize, Environment.ProcessorCount, 0);

        public readonly int PacketSize;

        public readonly int ConcurrentAcceptCalls;

        public readonly ushort PreallocatedTransmissionArgs;

        public StreamSocketServerOptions(int packetSize, int concurrentAcceptCalls, ushort preallocatedTransmissionArgs)
        {
            PacketSize = packetSize;

            ConcurrentAcceptCalls = concurrentAcceptCalls;

            PreallocatedTransmissionArgs = preallocatedTransmissionArgs;
        }
    }

    //TODO address the need to handle series of network packets, not just single packets
    //TODO allow for the server to do more than just echo packets
    //TODO document class
    public sealed class StreamSocketServer : SocketServer
    {
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

        private readonly StreamSocketServerOptions serverOptions;

        /// <summary>
        /// Constructs a new instance of the <see cref="StreamSocketServer"/> class.
        /// </summary>
        /// <param name="serverOptions">Additional options to configure the server.</param>
        /// <inheritdoc />
        public StreamSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in SocketServerPacketHandler packetHandler, in StreamSocketServerOptions? serverOptions = null)
            : base(in connectionAddressFamily, SocketType.Stream, in connectionProtocolType, in packetHandler,
            serverOptions?.PacketSize ?? StreamSocketServerOptions.Defaults.PacketSize,
            serverOptions?.PreallocatedTransmissionArgs ?? StreamSocketServerOptions.Defaults.PreallocatedTransmissionArgs)
        {
            this.serverOptions = serverOptions ?? StreamSocketServerOptions.Defaults;
        }

        /// <inheritdoc />
        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs();

            connectionArgs.Completed += HandleIoCompleted;

            return connectionArgs;
        }

        /// <inheritdoc />
        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {
        }

        /// <inheritdoc />
        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
        {
            return true;
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
                    SocketAsyncEventArgs newAcceptArgs = TransmissionArgsPool.Rent();

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

        private void Accept(SocketAsyncEventArgs acceptArgs)
        {
            bool operationPending = Connection.AcceptAsync(acceptArgs);

            if (operationPending) return;

            SocketAsyncEventArgs newAcceptArgs = TransmissionArgsPool.Rent();

            Accept(newAcceptArgs); // start a new accept operation to not miss any clients

            CompleteAccept(acceptArgs);
        }

        private void CompleteAccept(SocketAsyncEventArgs connectedClientArgs)
        {
            Socket clientSocket = connectedClientArgs.AcceptSocket;

            RemoteStreamClientToken clientToken = new RemoteStreamClientToken(in clientSocket);

            connectedClientArgs.UserToken = clientToken;

            Receive(connectedClientArgs);
        }

        private void Receive(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;

            byte[] requestBuffer = BufferPool.Rent(serverOptions.PacketSize);
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            clientToken.RentedBuffer = requestBuffer;
            clientArgs.SetBuffer(clientToken.RentedBuffer, 0, serverOptions.PacketSize);

            bool operationPending = clientToken.ClientSocket.ReceiveAsync(clientArgs);

            if (!operationPending)
            {
                CompleteReceive(clientArgs);
            }
        }

        private void CompleteReceive(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken receiveToken = (RemoteStreamClientToken)clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == serverOptions.PacketSize)
                {
                    // buffer was fully received

                    NetworkPacket request = NetworkPacket.Deserialise(receiveToken.RentedBuffer);

                    NetworkPacket response = PacketHandler(in request, clientArgs.RemoteEndPoint);

                    if (!response.Equals(NetworkPacket.NullPacket))
                    {
                        byte[] responseBuffer = BufferPool.Rent(serverOptions.PacketSize);
                        Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                        NetworkPacket.Serialise(response, responseBufferMemory);

                        BufferPool.Return(receiveToken.RentedBuffer, true); // at this point the request buffer can be returned

                        receiveToken.RentedBuffer = responseBuffer;
                        clientArgs.SetBuffer(responseBuffer, 0, serverOptions.PacketSize);

                        Send(clientArgs);
                    }
                    else
                    {
                        BufferPool.Return(receiveToken.RentedBuffer, true); // at this point the request buffer can be returned

                        Receive(clientArgs);
                    }
                }
                else if (serverOptions.PacketSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // receive the remaining parts of the buffer

                    int receivedBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(receivedBytes, serverOptions.PacketSize - receivedBytes);

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

        private void Send(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;

            bool operationPending = clientToken.ClientSocket.SendAsync(clientArgs);

            if (!operationPending)
            {
                CompleteSend(clientArgs);
            }
        }

        private void CompleteSend(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken sendToken = (RemoteStreamClientToken)clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == serverOptions.PacketSize)
                {
                    // buffer was fully sent

                    BufferPool.Return(sendToken.RentedBuffer, true);

                    sendToken.RentedBuffer = null;

                    Receive(clientArgs);
                }
                else if (serverOptions.PacketSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // send the remaining parts of the buffer

                    int sentBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(sentBytes, serverOptions.PacketSize - sentBytes);

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

        private void CloseClientSocket(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken)clientArgs.UserToken;
            clientToken.Dispose();

            TransmissionArgsPool.Return(clientArgs);
        }

        public ref readonly StreamSocketServerOptions ServerOptions
        {
            get { return ref serverOptions; }
        }

        /// <inheritdoc />
        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            Connection.Listen(100);

            for (int i = 0; i < serverOptions.ConcurrentAcceptCalls; i++)
            {
                SocketAsyncEventArgs acceptArgs = TransmissionArgsPool.Rent();

                Accept(acceptArgs);
            }

            cancellationToken.WaitHandle.WaitOne();

            return Task.CompletedTask;
        }
    }
}