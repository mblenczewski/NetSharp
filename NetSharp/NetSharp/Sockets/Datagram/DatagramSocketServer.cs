using NetSharp.Utils;

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

    //TODO address the need for a fixed packet size (NetworkPacket.TotalSize; lines 109 and 145)
    //TODO address the need for a fixed number of initial ReceiveFrom method calls
    //TODO address the need to handle series of network packets, not just single packets
    public sealed class DatagramSocketServer : SocketServer
    {
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public readonly DatagramSocketServerOptions ServerOptions;

        public DatagramSocketServer(in AddressFamily connectionAddressFamily, in ProtocolType connectionProtocolType,
            in DatagramSocketServerOptions serverOptions = default) : base(in connectionAddressFamily, SocketType.Dgram,
            in connectionProtocolType)
        {
            ServerOptions = serverOptions.Equals(default) ? DatagramSocketServerOptions.Defaults : serverOptions;
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
                case SocketAsyncOperation.SendTo:
                    CompleteSendTo(args);
                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    ReceiveFrom(AnyRemoteEndPoint); // start a new receive from operation immediately, to not drop any packets

                    CompleteReceiveFrom(args);
                    break;

                default:
                    throw new NotSupportedException($"{nameof(HandleIoCompleted)} doesn't support {args.LastOperation}");
            }
        }

        private void SendTo(SocketAsyncEventArgs sendArgs)
        {
            if (!connection.SendToAsync(sendArgs))
            {
                CompleteSendTo(sendArgs);
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs sendArgs)
        {
            SocketOperationToken sendToken = (SocketOperationToken) sendArgs.UserToken;

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

        private void ReceiveFrom(EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs args = TransmissionArgsPool.Rent();

            byte[] receiveBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            args.SetBuffer(receiveBufferMemory);
            args.RemoteEndPoint = remoteEndPoint;
            args.UserToken = new SocketOperationToken(in receiveBuffer);

            if (!connection.ReceiveFromAsync(args))
            {
                ReceiveFrom(AnyRemoteEndPoint); // start a new receive from operation immediately, to not drop any packets

                CompleteReceiveFrom(args);
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

            SocketAsyncEventArgs sendArgs = TransmissionArgsPool.Rent();

            byte[] sendBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> sendBufferMemory = new Memory<byte>(sendBuffer);

            NetworkPacket.Serialise(response, sendBufferMemory);

            sendArgs.SetBuffer(sendBufferMemory);
            sendArgs.RemoteEndPoint = receiveResult.RemoteEndPoint;
            sendArgs.UserToken = new SocketOperationToken(in sendBuffer);

            SendTo(sendArgs);

            BufferPool.Return(receiveToken.RentedBuffer, true);

            TransmissionArgsPool.Return(receiveArgs);
        }

        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 10; i++)
            {
                ReceiveFrom(AnyRemoteEndPoint);
            }

            return cancellationToken.WaitHandle.WaitOneAsync();
        }
    }
}