using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using NetSharp.Packets;
using NetSharp.Utils.Conversion;

namespace NetSharp.Utils
{
    /// <summary>
    /// Helper class for asynchronously performing common network operations, for both the UDP and TCP protocols.
    /// </summary>
    internal static class NetworkOperations
    {
        /// <summary>
        /// Reads the specified amount of data from the network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read data from the network.</param>
        /// <param name="count">The number of bytes to read from the network.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <returns>The result of the receive operation.</returns>
        private static TransmissionResult Read(Socket socket, int count, SocketFlags socketFlags)
        {
            byte[] byteBuffer = new byte[count];
            int receivedBytesCount = 0;

            while (count > receivedBytesCount)
            {
                Span<byte> receivedBytes = new Span<byte>(byteBuffer, receivedBytesCount, count - receivedBytesCount);

                receivedBytesCount += socket.Receive(receivedBytes, socketFlags);
            }

            return new TransmissionResult(byteBuffer, receivedBytesCount, socket.RemoteEndPoint); ;
        }

        /// <summary>
        /// Reads a datagram segment of the given length from the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read data from the network.</param>
        /// <param name="count">The number of bytes to read from the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint from which data should be read.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <returns>The result of the receive operation.</returns>
        private static TransmissionResult ReadFrom(Socket socket, int count, EndPoint remoteEndPoint, SocketFlags socketFlags)
        {
            byte[] byteBuffer = new byte[count];
            EndPoint actualRemoteEndPoint = remoteEndPoint;
            int receivedBytesCount = 0;

            while (count > receivedBytesCount)
            {
                receivedBytesCount +=
                    socket.ReceiveMessageFrom(byteBuffer, receivedBytesCount, count - receivedBytesCount,
                        ref socketFlags, ref actualRemoteEndPoint, out IPPacketInformation _);
            }

            return new TransmissionResult(byteBuffer, receivedBytesCount, actualRemoteEndPoint);
        }

        /// <summary>
        /// Writes the given data buffer to the network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write, and the given <see cref="CancellationToken"/>
        /// is used to allow for asynchronous task cancellation.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="buffer">The buffer that should be written to the network.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        private static void Write(Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            int bytesToSend = buffer.Length;
            int sentBytesCount = 0;

            while (bytesToSend > sentBytesCount)
            {
                ReadOnlySpan<byte> bufferSegment = buffer.Span.Slice(sentBytesCount, bytesToSend - sentBytesCount);

                sentBytesCount += socket.Send(bufferSegment, socketFlags);
            }
        }

        /// <summary>
        /// Writes the given data buffer to the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write, and the given <see cref="CancellationToken"/>
        /// is used to allow for asynchronous task cancellation.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which data should be written.</param>
        /// <param name="buffer">The buffer that should be written to the network.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        private static void WriteTo(Socket socket, EndPoint remoteEndPoint, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
        {
            int bytesToSend = buffer.Length;
            int sentBytesCount = 0;

            while (bytesToSend > sentBytesCount)
            {
                ReadOnlySpan<byte> bufferSegment = buffer.Span.Slice(sentBytesCount, bytesToSend - sentBytesCount);

                sentBytesCount += socket.SendTo(bufferSegment.ToArray(), socketFlags, remoteEndPoint);
            }
        }

        /// <summary>
        /// Reads a packet from network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read the packet from the network.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <returns>The read packet.</returns>
        internal static Packet ReadPacket(Socket socket, SocketFlags socketFlags)
        {
            TransmissionResult packetHeaderResult = Read(socket, Packet.HeaderSize, socketFlags);

            int packetSize = EndianAwareBitConverter.ToInt32(packetHeaderResult.Buffer.Span.Slice(0, sizeof(int)));

            if (packetSize == 0)
            {
                return Packet.Deserialise(packetHeaderResult.Buffer);
            }

            TransmissionResult packetDataResult = Read(socket, packetSize, socketFlags);

            byte[] serialisedPacket = new byte[Packet.HeaderSize + packetSize];

            Memory<byte> serialisedPacketHeader = new Memory<byte>(serialisedPacket, 0, Packet.HeaderSize);
            packetHeaderResult.Buffer.CopyTo(serialisedPacketHeader);

            Memory<byte> serialisedPacketData = new Memory<byte>(serialisedPacket, Packet.HeaderSize, packetSize);
            packetDataResult.Buffer.CopyTo(serialisedPacketData);

            return Packet.Deserialise(serialisedPacket);
        }

        /// <summary>
        /// Reads a packet from the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read the packet from the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint from which a packet should be read.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <returns>The read packet and associated transmission results.</returns>
        internal static (Packet packet, TransmissionResult packetResult) ReadPacketFrom(
            Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags)
        {
            TransmissionResult packetHeaderResult =
                ReadFrom(socket, Packet.HeaderSize, remoteEndPoint, socketFlags);

            int packetSize = EndianAwareBitConverter.ToInt32(packetHeaderResult.Buffer.Span.Slice(0, sizeof(int)));

            if (packetSize == 0)
            {
                return (Packet.Deserialise(packetHeaderResult.Buffer), packetHeaderResult);
            }

            TransmissionResult packetDataResult =
                ReadFrom(socket, packetSize, packetHeaderResult.RemoteEndPoint, socketFlags);

            byte[] serialisedPacket = new byte[Packet.HeaderSize + packetSize];

            Memory<byte> serialisedPacketHeader = new Memory<byte>(serialisedPacket, 0, Packet.HeaderSize);
            packetHeaderResult.Buffer.CopyTo(serialisedPacketHeader);

            Memory<byte> serialisedPacketData = new Memory<byte>(serialisedPacket, Packet.HeaderSize, packetSize);
            packetDataResult.Buffer.CopyTo(serialisedPacketData);

            return (Packet.Deserialise(serialisedPacket), packetDataResult);
        }

        /// <summary>
        /// Writes the given packet to the network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="packet">The packet that should be written to the network.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        internal static void WritePacket(Socket socket, Packet packet, SocketFlags socketFlags)
            => Write(socket, Packet.Serialise(packet), socketFlags);

        /// <summary>
        /// Writes the given packet to the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which data should be written.</param>
        /// <param name="packet">The packet that should be written to the remote endpoint.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        internal static void WritePacketTo(Socket socket, EndPoint remoteEndPoint, Packet packet, SocketFlags socketFlags)
            => WriteTo(socket, remoteEndPoint, Packet.Serialise(packet), socketFlags);
    }
}