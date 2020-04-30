using NetSharp.Packets;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetSharp.Sockets.Datagram
{
    /// <summary>
    /// Provides additional configuration options for a <see cref="DatagramSocketServer" /> instance.
    /// </summary>
    public readonly struct DatagramSocketServerOptions
    {
        /// <summary>
        /// The default configuration.
        /// </summary>
        public static readonly DatagramSocketServerOptions Defaults =
            new DatagramSocketServerOptions(Environment.ProcessorCount, 0);

        /// <summary>
        /// The number of <see cref="Socket.ReceiveFromAsync" /> calls that will be 'in-flight' at any one time, and ready to service incoming client
        /// packets. This should be set to the number of client which will be connected at once.
        /// </summary>
        public readonly int ConcurrentReceiveFromCalls;

        /// <summary>
        /// The number of <see cref="SocketAsyncEventArgs" /> instances that should be preallocated for use in the <see cref="Socket.SendToAsync" />
        /// and <see cref="Socket.ReceiveFromAsync" /> methods.
        /// </summary>
        public readonly ushort PreallocatedTransmissionArgs;

        /// <summary>
        /// Constructs a new instance of the <see cref="DatagramSocketServerOptions" /> struct.
        /// </summary>
        /// <param name="concurrentReceiveFromCalls">
        /// The number of <see cref="Socket.ReceiveFromAsync" /> calls which should be 'in-flight' at any one time.
        /// </param>
        /// <param name="preallocatedTransmissionArgs">
        /// The number of <see cref="SocketAsyncEventArgs" /> instances to preallocate.
        /// </param>
        public DatagramSocketServerOptions(int concurrentReceiveFromCalls, ushort preallocatedTransmissionArgs)
        {
            ConcurrentReceiveFromCalls = concurrentReceiveFromCalls;

            PreallocatedTransmissionArgs = preallocatedTransmissionArgs;
        }
    }

    //TODO address the need to handle series of network packets, not just single packets
    //TODO document class
    public sealed class DatagramSocketServer : RawSocketServer
    {
        private static readonly EndPoint AnyRemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly DatagramSocketServerOptions serverOptions;

        private CancellationToken serverShutdownToken;

        /// <summary>
        /// Constructs a new instance of the <see cref="DatagramSocketServer" /> class.
        /// </summary>
        /// <param name="serverOptions">
        /// Additional options to configure the server.
        /// </param>
        /// <inheritdoc />
        public DatagramSocketServer(ref Socket rawConnection, in RawRequestPacketHandler packetHandler, in DatagramSocketServerOptions? serverOptions = null)
            : base(ref rawConnection, 
                NetworkPacket.TotalSize,
                serverOptions?.PreallocatedTransmissionArgs ?? DatagramSocketServerOptions.Defaults.PreallocatedTransmissionArgs,
                packetHandler)
        {
            if (rawConnection.SocketType != SocketType.Dgram)
            {
                throw new ArgumentException($"Only {SocketType.Dgram} is supported!", nameof(rawConnection));
            }

            this.serverOptions = serverOptions ?? DatagramSocketServerOptions.Defaults;
        }

        public ref readonly DatagramSocketServerOptions ServerOptions
        {
            get { return ref serverOptions; }
        }

        private void CompleteReceiveFrom(SocketAsyncEventArgs receiveArgs)
        {
            SocketOperationToken receiveToken = (SocketOperationToken)receiveArgs.UserToken;

            if (receiveArgs.SocketError == SocketError.Success)
            {
                byte[] responseBuffer = BufferPool.Rent(MaxBufferSize);

                bool responseExists = PacketHandler(receiveArgs.RemoteEndPoint, receiveToken.RentedBuffer, responseBuffer);
                BufferPool.Return(receiveToken.RentedBuffer, true);

                //NetworkPacket.Deserialise(receiveArgs.MemoryBuffer, out NetworkPacket request);
                //NetworkPacket response = PacketHandler(in request, receiveArgs.RemoteEndPoint);

                if (responseExists)
                {
                    //byte[] sendBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
                    //Memory<byte> sendBufferMemory = new Memory<byte>(sendBuffer);
                    //NetworkPacket.Serialise(response, sendBufferMemory);

                    receiveArgs.SetBuffer(responseBuffer);
                    receiveArgs.UserToken = new SocketOperationToken(ref responseBuffer);

                    SendTo(receiveArgs);
                }
                else
                {
                    BufferPool.Return(responseBuffer, true);
                }
            }
            else
            {
                BufferPool.Return(receiveToken.RentedBuffer, true);

                ArgsPool.Return(receiveArgs);
            }
        }

        private void CompleteSendTo(SocketAsyncEventArgs sendArgs)
        {
            SocketOperationToken sendToken = (SocketOperationToken)sendArgs.UserToken;

            BufferPool.Return(sendToken.RentedBuffer, true);

            ArgsPool.Return(sendArgs);
        }

        private void ReceiveFrom(SocketAsyncEventArgs receiveArgs)
        {
            if (serverShutdownToken.IsCancellationRequested)
            {
                ArgsPool.Return(receiveArgs);

                return;
            }

            byte[] receiveBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            Memory<byte> receiveBufferMemory = new Memory<byte>(receiveBuffer);

            receiveArgs.SetBuffer(receiveBufferMemory);
            receiveArgs.UserToken = new SocketOperationToken(ref receiveBuffer);

            bool operationPending = Connection.ReceiveFromAsync(receiveArgs);

            if (operationPending) return;

            SocketAsyncEventArgs newReceiveArgs = ArgsPool.Rent();
            newReceiveArgs.RemoteEndPoint = AnyRemoteEndPoint;

            ReceiveFrom(newReceiveArgs); // start a new receive from operation immediately, to not drop any packets

            CompleteReceiveFrom(receiveArgs);
        }

        private void SendTo(SocketAsyncEventArgs sendArgs)
        {
            if (serverShutdownToken.IsCancellationRequested)
            {
                SocketOperationToken sendToken = (SocketOperationToken)sendArgs.UserToken;

                BufferPool.Return(sendToken.RentedBuffer, true);

                ArgsPool.Return(sendArgs);

                return;
            }

            bool operationPending = Connection.SendToAsync(sendArgs);

            if (!operationPending)
            {
                CompleteSendTo(sendArgs);
            }
        }

        /// <inheritdoc />
        protected override bool CanTransmissionArgsBeReused(in SocketAsyncEventArgs args)
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
                case SocketAsyncOperation.ReceiveFrom:
                    SocketAsyncEventArgs newReceiveArgs = ArgsPool.Rent();
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

        /// <inheritdoc />
        protected override void ResetTransmissionArgs(ref SocketAsyncEventArgs args)
        {
        }

        /// <inheritdoc />
        public override Task RunAsync(CancellationToken cancellationToken = default)
        {
            serverShutdownToken = cancellationToken;

            for (int i = 0; i < serverOptions.ConcurrentReceiveFromCalls; i++)
            {
                SocketAsyncEventArgs newReceiveArgs = ArgsPool.Rent();
                newReceiveArgs.RemoteEndPoint = AnyRemoteEndPoint;

                ReceiveFrom(newReceiveArgs);
            }

            serverShutdownToken.WaitHandle.WaitOne();

            return Task.CompletedTask;
        }

        private readonly struct SocketOperationToken
        {
            public readonly byte[] RentedBuffer;

            public SocketOperationToken(ref byte[] rentedBuffer)
            {
                RentedBuffer = rentedBuffer;
            }
        }
    }
}