using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw.Stream
{
    public sealed class FixedPacketRawStreamNetworkReader : RawStreamNetworkReader
    {
        private readonly int messageSize;

        /// <inheritdoc />
        public FixedPacketRawStreamNetworkReader(ref Socket rawConnection, RawStreamRequestHandler? requestHandler, EndPoint defaultEndPoint, int messageSize,
            int pooledBuffersPerBucket = 50, uint preallocatedStateObjects = 0) : base(ref rawConnection, requestHandler, defaultEndPoint, messageSize,
            pooledBuffersPerBucket, preallocatedStateObjects)
        {
            this.messageSize = messageSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureReceive(SocketAsyncEventArgs args, int dataSize)
        {
            byte[] receiveBuffer = BufferPool.Rent(dataSize);
            args.SetBuffer(receiveBuffer, 0, dataSize);

            TransmissionToken token = new TransmissionToken(dataSize, 0);
            args.UserToken = token;
        }

        /// <inheritdoc />
        protected override void CompleteAccept(SocketAsyncEventArgs args)
        {
            switch (args.SocketError)
            {
                case SocketError.Success:
                    ConfigureReceive(args, messageSize);
                    StartReceive(args);
                    break;

                default:
                    ArgsPool.Return(args);
                    break;
            }

            StartDefaultAccept();
        }

        /// <inheritdoc />
        protected override void CompleteReceive(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken)args.UserToken;

            byte[] receiveBuffer = args.Buffer;

            int expectedBytes = token.ExpectedBytes;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int receivedBytes = args.BytesTransferred, previousReceivedBytes = token.BytesTransferred, totalReceivedBytes = previousReceivedBytes + receivedBytes;

                    if (totalReceivedBytes == expectedBytes)  // transmission complete
                    {
                        EndPoint clientEndPoint = args.AcceptSocket.RemoteEndPoint;

                        byte[] responseBuffer = BufferPool.Rent(messageSize);

                        bool responseExists = RequestHandler(clientEndPoint, receiveBuffer, totalReceivedBytes, responseBuffer);
                        BufferPool.Return(receiveBuffer, true);

                        if (responseExists)
                        {
                            args.SetBuffer(responseBuffer, 0, messageSize);

                            TransmissionToken sendToken = new TransmissionToken(messageSize, 0);
                            args.UserToken = sendToken;

                            StartSend(args);
                            return;
                        }

                        BufferPool.Return(responseBuffer, true);

                        ConfigureReceive(args, messageSize);
                        StartReceive(args);
                    }
                    else if (0 < totalReceivedBytes && totalReceivedBytes < expectedBytes)  // transmission not complete
                    {
                        token = new TransmissionToken(in token, receivedBytes);
                        args.UserToken = token;

                        args.SetBuffer(totalReceivedBytes, expectedBytes - totalReceivedBytes);

                        ContinueReceive(args);
                    }
                    else if (receivedBytes == 0)  // connection is dead
                    {
                        CloseClientConnection(args);
                    }
                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }

        /// <inheritdoc />
        protected override void CompleteSend(SocketAsyncEventArgs args)
        {
            TransmissionToken token = (TransmissionToken)args.UserToken;

            byte[] sendBuffer = args.Buffer;
            int expectedBytes = token.ExpectedBytes;

            switch (args.SocketError)
            {
                case SocketError.Success:
                    int sentBytes = args.BytesTransferred, previousSentBytes = token.BytesTransferred, totalSentBytes = previousSentBytes + sentBytes;

                    if (totalSentBytes == expectedBytes)  // transmission complete
                    {
                        BufferPool.Return(sendBuffer, true);

                        ConfigureReceive(args, messageSize);
                        StartReceive(args);
                    }
                    else if (0 < totalSentBytes && totalSentBytes < expectedBytes)  // transmission not complete
                    {
                        token = new TransmissionToken(in token, sentBytes);
                        args.UserToken = token;

                        args.SetBuffer(totalSentBytes, expectedBytes - totalSentBytes);

                        ContinueSend(args);
                    }
                    else if (sentBytes == 0)  // connection is dead
                    {
                        CloseClientConnection(args);
                    }
                    break;

                default:
                    CloseClientConnection(args);
                    break;
            }
        }
    }
}