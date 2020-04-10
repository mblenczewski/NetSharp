﻿using NetSharp.Packets;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSharp.Utils;

namespace NetSharp.Sockets
{
    public abstract class SocketClient : SocketConnection
    {
        protected readonly SocketAsyncEventArgs Args;

        protected SocketClient(in AddressFamily connectionAddressFamily, in SocketType connectionSocketType, in ProtocolType connectionProtocolType)
            : base(in connectionAddressFamily, in connectionSocketType, in connectionProtocolType)
        {
            Args = new SocketAsyncEventArgs();
            Args.Completed += SocketAsyncOperations.HandleIoCompleted;
        }

        public int SendBytes(Memory<byte> outgoingDataBuffer, SocketFlags flags = SocketFlags.None)
        {
            byte[] temporaryBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            outgoingDataBuffer.CopyTo(temporaryBuffer);
            int sentBytes = connection.Send(temporaryBuffer);
            BufferPool.Return(temporaryBuffer);

            return sentBytes;
        }

        public int ReceiveBytes(Memory<byte> incomingDataBuffer, SocketFlags flags = SocketFlags.None)
        {
            byte[] temporaryBuffer = BufferPool.Rent(NetworkPacket.TotalSize);
            int receivedBytes = connection.Receive(temporaryBuffer);
            temporaryBuffer.CopyTo(incomingDataBuffer);
            BufferPool.Return(temporaryBuffer);

            return receivedBytes;
        }
    }
}