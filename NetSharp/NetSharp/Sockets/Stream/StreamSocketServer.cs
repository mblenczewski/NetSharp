using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Utils;

namespace NetSharp.Sockets.Stream
{
    //TODO document
    public readonly struct StreamSocketServerOptions
    {
        public static readonly StreamSocketServerOptions Defaults =
            new StreamSocketServerOptions(NetworkPacket.TotalSize, 8);

        public readonly int PacketSize;

        public readonly int ConcurrentAcceptCalls;

        public StreamSocketServerOptions(int packetSize, int concurrentAcceptCalls)
        {
            PacketSize = packetSize;

            ConcurrentAcceptCalls = concurrentAcceptCalls;
        }
    }

    //TODO fix memory leak issue
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
                ClientSocket.Dispose();
            }
        }

        public readonly StreamSocketServerOptions ServerOptions;

        public StreamSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in StreamSocketServerOptions? serverOptions = null) : base(in connectionAddressFamily, SocketType.Stream,
            in connectionProtocolType)
        {
            ServerOptions = serverOptions ?? StreamSocketServerOptions.Defaults;
        }

        protected override SocketAsyncEventArgs CreateTransmissionArgs()
        {
            SocketAsyncEventArgs connectionArgs = new SocketAsyncEventArgs();

            connectionArgs.Completed += HandleIoCompleted;

            return connectionArgs;
        }

        protected override void ResetTransmissionArgs(SocketAsyncEventArgs args)
        {

        }

        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
        {
            return true;
        }

        protected override void DestroyTransmissionArgs(SocketAsyncEventArgs remoteConnectionArgs)
        {
            remoteConnectionArgs.AcceptSocket.Shutdown(SocketShutdown.Both);
            remoteConnectionArgs.AcceptSocket.Close();

            remoteConnectionArgs.Completed -= SocketAsyncOperations.HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

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
            bool operationPending = connection.AcceptAsync(acceptArgs);

            if (!operationPending)
            {
                SocketAsyncEventArgs newAcceptArgs = TransmissionArgsPool.Rent();

                Accept(newAcceptArgs); // start a new accept operation to not miss any clients

                CompleteAccept(acceptArgs);
            }
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
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken) clientArgs.UserToken;

            byte[] requestBuffer = BufferPool.Rent(ServerOptions.PacketSize);
            Memory<byte> requestBufferMemory = new Memory<byte>(requestBuffer);

            clientToken.RentedBuffer = requestBuffer;
            clientArgs.SetBuffer(clientToken.RentedBuffer, 0, ServerOptions.PacketSize);

            bool operationPending = clientToken.ClientSocket.ReceiveAsync(clientArgs);

            if (!operationPending)
            {
                CompleteReceive(clientArgs);
            }
        }

        private void CompleteReceive(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken receiveToken = (RemoteStreamClientToken) clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == ServerOptions.PacketSize)
                {
                    // buffer was fully received

                    NetworkPacket request = NetworkPacket.Deserialise(receiveToken.RentedBuffer);

                    // TODO implement actual request processing, not just an echo server
                    NetworkPacket response = request;

                    byte[] responseBuffer = BufferPool.Rent(ServerOptions.PacketSize);
                    Memory<byte> responseBufferMemory = new Memory<byte>(responseBuffer);

                    NetworkPacket.Serialise(response, responseBufferMemory);

                    BufferPool.Return(receiveToken.RentedBuffer, true); // at this point the request buffer can be returned

                    receiveToken.RentedBuffer = responseBuffer;
                    clientArgs.SetBuffer(responseBuffer, 0, ServerOptions.PacketSize);

                    Send(clientArgs);

                }
                else if (ServerOptions.PacketSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // receive the remaining parts of the buffer

                    int receivedBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(receivedBytes, ServerOptions.PacketSize - receivedBytes);

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
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken) clientArgs.UserToken;

            bool operationPending = clientToken.ClientSocket.SendAsync(clientArgs);

            if (!operationPending)
            {
                CompleteSend(clientArgs);
            }
        }

        private void CompleteSend(SocketAsyncEventArgs clientArgs)
        {
            RemoteStreamClientToken sendToken = (RemoteStreamClientToken) clientArgs.UserToken;

            if (clientArgs.SocketError == SocketError.Success)
            {
                if (clientArgs.BytesTransferred == ServerOptions.PacketSize)
                {
                    // buffer was fully sent

                    BufferPool.Return(sendToken.RentedBuffer, true);

                    sendToken.RentedBuffer = null;

                    Receive(clientArgs);
                }
                else if (ServerOptions.PacketSize > clientArgs.BytesTransferred && clientArgs.BytesTransferred > 0)
                {
                    // send the remaining parts of the buffer

                    int sentBytes = clientArgs.BytesTransferred;

                    clientArgs.SetBuffer(sentBytes, ServerOptions.PacketSize - sentBytes);

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
            RemoteStreamClientToken clientToken = (RemoteStreamClientToken) clientArgs.UserToken;

            try
            {
                clientToken.ClientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);
            }

            clientToken.ClientSocket.Close();

            TransmissionArgsPool.Return(clientArgs);

            clientToken.Dispose();
        }

        public override async Task RunAsync(CancellationToken cancellationToken = default)
        {
            connection.Listen(100);

            for (int i = 0; i < ServerOptions.ConcurrentAcceptCalls; i++)
            {
                SocketAsyncEventArgs acceptArgs = TransmissionArgsPool.Rent();
                
                Accept(acceptArgs);
            }

            await cancellationToken.WaitHandle.WaitOneAsync();
        }
    }
}