using NetSharp.Packets;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;

namespace NetSharp.Sockets
{
    public abstract class SocketClient : SocketConnection
    {
        protected readonly ArrayPool<byte> BufferPool;

        protected SocketClient(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
            BufferPool = ArrayPool<byte>.Create(NetworkPacket.TotalSize, 10);
        }

        public int SendBytes(Memory<byte> outgoingDataBuffer)
        {
            return connection.Send(outgoingDataBuffer.Span);
        }

        public int SendBytesTo(Memory<byte> outgoingDataBuffer, EndPoint remoteEndPoint)
        {
            return connection.SendTo(outgoingDataBuffer.ToArray(), remoteEndPoint);
        }

        public int ReceiveBytes(Memory<byte> incomingDataBuffer)
        {
            byte[] temporaryBuffer = new byte[NetworkPacket.TotalSize];
            int receivedBytes = connection.Receive(temporaryBuffer);
            temporaryBuffer.CopyTo(incomingDataBuffer);

            return receivedBytes;
        }

        public int ReceiveBytesFrom(Memory<byte> incomingDataBuffer, ref EndPoint remoteEndPoint)
        {
            byte[] temporaryBuffer = new byte[NetworkPacket.TotalSize];
            int receivedBytes = connection.ReceiveFrom(temporaryBuffer, ref remoteEndPoint);
            temporaryBuffer.CopyTo(incomingDataBuffer);

            return receivedBytes;
        }
    }
}