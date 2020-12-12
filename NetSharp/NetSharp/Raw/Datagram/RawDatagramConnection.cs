using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace NetSharp.Raw.Datagram
{
    public delegate void RawDatagramPacketHandler(
        EndPoint remoteEndPoint,
        in RawPacketHeader header,
        in ReadOnlyMemory<byte> data,
        IRawDatagramWriter writer);

    public interface IRawDatagramPacketHandler
    {
    }

    public interface IRawDatagramWriter : IRawDatagramPacketHandler
    {
        ValueTask<int> SendAsync(ushort type, ReadOnlyMemory<byte> data, SocketFlags flags = SocketFlags.None);
    }

    public sealed class RawDatagramConnection : RawConnectionBase, IRawDatagramWriter
    {
        /// <summary>
        /// The maximum size that a user supplied data buffer can be to fit into a UDP diagram with a preceding packet header.
        /// </summary>
        public const int MaxDatagramDataLength = ushort.MaxValue - 28 - RawPacketHeader.Length;

        public RawDatagramConnection(ProtocolType connectionProtocolType, EndPoint defaultRemoteEndPoint)
            : base(SocketType.Dgram, connectionProtocolType, defaultRemoteEndPoint)
        {
        }

        /// <inheritdoc />
        public ValueTask<int> SendAsync(ushort type, ReadOnlyMemory<byte> data, SocketFlags flags = SocketFlags.None)
        {
            return new ValueTask<int>(0);
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
        protected override void HandlerTaskWork()
        {
            // TODO: implement start read task for datagram connections
        }

        private void HandleIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    break;

                case SocketAsyncOperation.Disconnect:
                    break;

                case SocketAsyncOperation.ReceiveFrom:
                    break;

                case SocketAsyncOperation.SendTo:
                    break;

                default:
                    break;
            }
        }
    }
}
