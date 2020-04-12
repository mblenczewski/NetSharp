using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using NetworkPacket = NetSharp.Packets.NetworkPacket;

namespace NetSharp.Sockets.Datagram
{
    //TODO document
    public readonly struct DatagramSocketServerOptions
    {
        public static readonly DatagramSocketServerOptions Defaults =
            new DatagramSocketServerOptions(NetworkPacket.TotalSize, 8);

        public readonly int PacketSize;

        public readonly int ConcurrentReceiveFromCalls;

        public DatagramSocketServerOptions(int packetSize, int concurrentReceiveFromCalls)
        {
            PacketSize = packetSize;

            ConcurrentReceiveFromCalls = concurrentReceiveFromCalls;
        }
    }

    //TODO fix memory leak issue
    //TODO address the need to handle series of network packets, not just single packets
    //TODO allow for the server to do more than just echo packets
    //TODO document class
    public sealed class DatagramSocketServer : SocketServer
    {
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public readonly DatagramSocketServerOptions ServerOptions;

        public DatagramSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in DatagramSocketServerOptions? serverOptions = null) : base(in connectionAddressFamily, SocketType.Dgram,
            in connectionProtocolType)
        {
            ServerOptions = serverOptions ?? DatagramSocketServerOptions.Defaults;
        }

        private readonly struct SocketOperationToken
        {
            public readonly byte[] RentedBuffer;

            public SocketOperationToken(in byte[] rentedBuffer)
            {
                RentedBuffer = rentedBuffer;
            }
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
            remoteConnectionArgs.Completed -= HandleIoCompleted;

            remoteConnectionArgs.Dispose();
        }

        protected override void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.ReceiveFrom:
                    SocketAsyncEventArgs newReceiveArgs = TransmissionArgsPool.Rent();
                    newReceiveArgs.RemoteEndPoint = AnyRemoteEndPoint;

                    ReceiveFrom(newReceiveArgs); // start a new receive from operation immediately, to not drop any packets

                    CompleteReceiveFrom(args);
                    break;

                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);
                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        private void ReceiveFrom(SocketAsyncEventArgs receiveArgs)
        {
            byte[] receiveBuffer = BufferPool.Rent(ServerOptions.PacketSize);
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            receiveArgs.SetBuffer(receiveBufferMemory);
            receiveArgs.UserToken = new SocketOperationToken(in receiveBuffer);

            bool operationPending = connection.ReceiveFromAsync(receiveArgs);

            if (!operationPending)
            {
                SocketAsyncEventArgs newReceiveArgs = TransmissionArgsPool.Rent();
                newReceiveArgs.RemoteEndPoint = AnyRemoteEndPoint;

                ReceiveFrom(newReceiveArgs); // start a new receive from operation immediately, to not drop any packets

                CompleteReceiveFrom(receiveArgs);
            }
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs receiveArgs)
        {
            SocketOperationToken receiveToken = (SocketOperationToken)receiveArgs.UserToken;

            TransmissionResult receiveResult = new TransmissionResult(in receiveArgs);

#if DEBUG
                lock (typeof(Console))
                {
                    Console.WriteLine($"[Server] Received {receiveResult.Count} bytes from {receiveResult.RemoteEndPoint}");
                    Console.WriteLine($"[Server] <<<< {Encoding.UTF8.GetString(receiveResult.Buffer.Span)}");
                }
#endif

            NetworkPacket request = NetworkPacket.Deserialise(receiveArgs.MemoryBuffer);

            // TODO implement actual request processing, not just an echo server
            NetworkPacket response = request;

            byte[] sendBuffer = BufferPool.Rent(ServerOptions.PacketSize);
            Memory<byte> sendBufferMemory = new Memory<byte>(sendBuffer);

            NetworkPacket.Serialise(response, sendBufferMemory);

            receiveArgs.SetBuffer(sendBufferMemory);
            receiveArgs.UserToken = new SocketOperationToken(in sendBuffer);

            SendTo(receiveArgs);

            BufferPool.Return(receiveToken.RentedBuffer, true);
        }

        private void SendTo(SocketAsyncEventArgs sendArgs)
        {
            bool operationPending = connection.SendToAsync(sendArgs);

            if (!operationPending)
            {
                CompleteSendTo(sendArgs);
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs sendArgs)
        {
            SocketOperationToken sendToken = (SocketOperationToken)sendArgs.UserToken;

            TransmissionResult sendResult = new TransmissionResult(in sendArgs);

#if DEBUG
                    lock (typeof(Console))
                    {
                        Console.WriteLine($"[Server] Sent {sendResult.Count} bytes to {sendResult.RemoteEndPoint}");
                        Console.WriteLine($"[Server] >>>> {Encoding.UTF8.GetString(sendResult.Buffer.Span)}");
                    }
#endif

            BufferPool.Return(sendToken.RentedBuffer, true);

            TransmissionArgsPool.Return(sendArgs);
        }

        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < ServerOptions.ConcurrentReceiveFromCalls; i++)
            {
                SocketAsyncEventArgs newReceiveArgs = TransmissionArgsPool.Rent();
                newReceiveArgs.RemoteEndPoint = AnyRemoteEndPoint;

                ReceiveFrom(newReceiveArgs);
            }

            return cancellationToken.WaitHandle.WaitOneAsync(CancellationToken.None);
        }
    }
}