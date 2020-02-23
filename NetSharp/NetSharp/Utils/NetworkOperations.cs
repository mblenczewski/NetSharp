using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using NetSharp.Extensions;
using NetSharp.Packets;
using NetSharp.Utils.Conversion;

namespace NetSharp.Utils
{
    /// <summary>
    /// Helper class for asynchronously performing common network operations, for both the UDP and TCP protocols.
    /// </summary>
    /// TODO: Implement cancellation support for network operations
    /// TODO: Somehow dont run into the exception below
    /// [Excep] Socket exception while reading bytes from 0.0.0.0:0: System.Net.Sockets.SocketException (10040): Komunikat wys┼éany na gniazdo datagramu by┼é wi─Ökszy ni┼╝ wewn─Ötrzny bufor lub przekracza┼é inny sieciowy limit albo bufor u┼╝ywany do odbierania datagram├│w by┼é mniejszy ni┼╝ sam datagram.
    /// at NetSharp.Extensions.SocketTask.GetResult() in G:\Git Repos\EnderRifter\NetSharp\NetSharp\NetSharp\Extensions\SocketExtensions.cs:line 158
    /// at NetSharp.Utils.NetworkOperations.ReadFromAsync(Socket socket, Int32 count, EndPoint remoteEndPoint, SocketFlags socketFlags) in G:\Git Repos\EnderRifter\NetSharp\NetSharp\NetSharp\Utils\NetworkOperations.cs:line 78
    /// at NetSharp.Utils.NetworkOperations.ReadPacketFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags, CancellationToken cancellationToken) in G:\Git Repos\EnderRifter\NetSharp\NetSharp\NetSharp\Utils\NetworkOperations.cs:line 225
    /// at NetSharp.Connection.DoReceivePacketFromAsync(Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags, TimeSpan timeout, CancellationToken cancellationToken) in G:\Git Repos\EnderRifter\NetSharp\NetSharp\NetSharp\Connection.cs:line 114
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
        private static Task<TransmissionResult> ReadAsync(Socket socket, int count, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            /*
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[count], 0, count);
            args.SocketFlags = socketFlags;
            SocketTask awaitableTask = new SocketTask(args);

            while (count > args.BytesTransferred)
            {
                await socket.ReceiveAsync(awaitableTask);
            }

            return new TransmissionResult(args.MemoryBuffer, args.BytesTransferred, socket.RemoteEndPoint);
            */

            return Task.Factory.StartNew(() =>
            {
                byte[] byteBuffer = new byte[count];
                int receivedBytesCount = 0;

                while (count > receivedBytesCount)
                {
                    Span<byte> receivedBytes =
                        new Span<byte>(byteBuffer, receivedBytesCount, count - receivedBytesCount);

                    receivedBytesCount += socket.Receive(receivedBytes, socketFlags);
                }

                return new TransmissionResult(byteBuffer, receivedBytesCount, socket.RemoteEndPoint);
            }, cancellationToken);
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
        private static Task<TransmissionResult> ReadFromAsync(Socket socket, int count, EndPoint remoteEndPoint, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            /*
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(new byte[count], 0, count);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            SocketTask awaitableTask = new SocketTask(args);

            while (count > args.BytesTransferred)
            {
                await socket.ReceiveMessageFromAsync(awaitableTask);
            }

            return new TransmissionResult(args.MemoryBuffer, args.BytesTransferred, args.RemoteEndPoint);
            */

            return Task.Factory.StartNew(() =>
            {
                byte[] byteBuffer = new byte[count];
                EndPoint actualRemoteEndPoint = remoteEndPoint;
                int receivedBytesCount = 0;

                while (count > receivedBytesCount)
                {
                    receivedBytesCount +=
                        socket.ReceiveMessageFrom(byteBuffer, receivedBytesCount, count - receivedBytesCount,
                            ref socketFlags, ref actualRemoteEndPoint, out IPPacketInformation packetInformation);
                }

                return new TransmissionResult(byteBuffer, receivedBytesCount, actualRemoteEndPoint);
            }, cancellationToken);
        }

        /// <summary>
        /// Writes the given data buffer to the network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write, and the given <see cref="CancellationToken"/>
        /// is used to allow for asynchronous task cancellation.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="buffer">The buffer that should be written to the network.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        private static Task WriteAsync(Socket socket, Memory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            /*
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(buffer);
            args.SocketFlags = socketFlags;
            SocketTask awaitableTask = new SocketTask(args);

            int bytesToSend = buffer.Length;
            while (bytesToSend > args.BytesTransferred)
            {
                await socket.SendAsync(awaitableTask);
            }
            */

            return Task.Factory.StartNew(() =>
            {
                int bytesToSend = buffer.Length;
                int sentBytesCount = 0;

                while (bytesToSend > sentBytesCount)
                {
                    ReadOnlySpan<byte> bufferSegment = buffer.Span.Slice(sentBytesCount, bytesToSend - sentBytesCount);

                    sentBytesCount += socket.Send(bufferSegment, socketFlags);
                }
            }, cancellationToken);
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
        private static Task WriteToAsync(Socket socket, EndPoint remoteEndPoint, Memory<byte> buffer, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            /*
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(buffer);
            args.SocketFlags = socketFlags;
            args.RemoteEndPoint = remoteEndPoint;
            SocketTask awaitableTask = new SocketTask(args);

            int bytesToSend = buffer.Length;
            while (bytesToSend > args.BytesTransferred)
            {
                await socket.SendToAsync(awaitableTask);
            }
            */

            return Task.Factory.StartNew(() =>
            {
                int bytesToSend = buffer.Length;
                int sentBytesCount = 0;

                while (bytesToSend > sentBytesCount)
                {
                    ReadOnlySpan<byte> bufferSegment = buffer.Span.Slice(sentBytesCount, bytesToSend - sentBytesCount);

                    sentBytesCount += socket.SendTo(bufferSegment.ToArray(), socketFlags, remoteEndPoint);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Reads a packet from network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read the packet from the network.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <param name="cancellationToken">The cancellation token that should be observed for the duration of the task.</param>
        /// <returns>The read packet, and the endpoint from which it was read.</returns>
        internal static async Task<(SerialisedPacket packet, EndPoint remoteEndPoint)> ReadPacketAsync(Socket socket, SocketFlags socketFlags,
            CancellationToken cancellationToken = default)
        {
            List<ReadOnlyMemory<byte>> userDataBuffer = new List<ReadOnlyMemory<byte>>(1);
            int receivedBytes = 0;

            EndPoint remoteEndPoint;
            NetworkPacket receivedPacket;
            uint receivedPacketType;

            do
            {
                TransmissionResult result = await ReadAsync(socket, NetworkPacket.PacketSize, socketFlags, cancellationToken);

                remoteEndPoint = result.RemoteEndPoint;
                receivedPacket = NetworkPacket.Deserialise(result.Buffer);
                receivedPacketType = receivedPacket.Header.Type;

                // TODO: try to remove this extra allocation
                userDataBuffer.Add(receivedPacket.DataBuffer);
                receivedBytes += receivedPacket.Header.DataLength;
            } while (receivedPacket.Footer.HasSucceedingPacket);

            Memory<byte> finalBuffer = new byte[receivedBytes];
            int writtenBytes = 0;

            foreach (ReadOnlyMemory<byte> bufferSegment in userDataBuffer)
            {
                bufferSegment.CopyTo(finalBuffer.Slice(writtenBytes, bufferSegment.Length));
                writtenBytes += bufferSegment.Length;
            }

            SerialisedPacket finalPacket = new SerialisedPacket(finalBuffer, receivedPacketType);

            return (finalPacket, remoteEndPoint);

            /*
            TransmissionResult packetHeaderResult =
                await ReadAsync(socket, NetworkPacket.HeaderSize, socketFlags, cancellationToken);

            int packetSize = EndianAwareBitConverter.ToInt32(packetHeaderResult.Buffer.Span.Slice(0, sizeof(int)));

            if (packetSize == 0)
            {
                return NetworkPacket.Deserialise(packetHeaderResult.Buffer);
            }

            TransmissionResult packetDataResult =
                await ReadAsync(socket, packetSize, socketFlags, cancellationToken);

            byte[] serialisedPacket = new byte[NetworkPacket.HeaderSize + packetSize];

            Memory<byte> serialisedPacketHeader = new Memory<byte>(serialisedPacket, 0, NetworkPacket.HeaderSize);
            packetHeaderResult.Buffer.CopyTo(serialisedPacketHeader);

            Memory<byte> serialisedPacketData = new Memory<byte>(serialisedPacket, NetworkPacket.HeaderSize, packetSize);
            packetDataResult.Buffer.CopyTo(serialisedPacketData);

            return NetworkPacket.Deserialise(serialisedPacket);
            */
        }

        /// <summary>
        /// Reads a packet from the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the read.
        /// </summary>
        /// <param name="socket">The socket which should read the packet from the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint from which a packet should be read.</param>
        /// <param name="socketFlags">The socket flags associated with the receive operation.</param>
        /// <param name="cancellationToken">The cancellation token that should be observed for the duration of the task.</param>
        /// <returns>The read packet, and the endpoint from which it was read.</returns>
        internal static async Task<(SerialisedPacket packet, EndPoint remoteEndPoint)> ReadPacketFromAsync(
            Socket socket, EndPoint remoteEndPoint, SocketFlags socketFlags, CancellationToken cancellationToken = default)
        {
            List<ReadOnlyMemory<byte>> userDataBuffer = new List<ReadOnlyMemory<byte>>(1);
            int receivedBytes = 0;

            NetworkPacket receivedPacket;
            uint receivedPacketType;

            do
            {
                TransmissionResult result =
                    await ReadFromAsync(socket, NetworkPacket.PacketSize, remoteEndPoint, socketFlags, cancellationToken);

                remoteEndPoint = result.RemoteEndPoint;
                receivedPacket = NetworkPacket.Deserialise(result.Buffer);
                receivedPacketType = receivedPacket.Header.Type;

                // TODO: try to remove this extra allocation
                userDataBuffer.Add(receivedPacket.DataBuffer);
                receivedBytes += receivedPacket.Header.DataLength;
            } while (receivedPacket.Footer.HasSucceedingPacket);

            Memory<byte> finalBuffer = new byte[receivedBytes];
            int writtenBytes = 0;

            foreach (ReadOnlyMemory<byte> bufferSegment in userDataBuffer)
            {
                bufferSegment.CopyTo(finalBuffer.Slice(writtenBytes, bufferSegment.Length));
                writtenBytes += bufferSegment.Length;
            }

            SerialisedPacket finalPacket = new SerialisedPacket(finalBuffer, receivedPacketType);

            return (finalPacket, remoteEndPoint);

            /*
            TransmissionResult packetHeaderResult =
                await ReadFromAsync(socket, NetworkPacket.HeaderSize, remoteEndPoint, socketFlags, cancellationToken);

            int packetSize = EndianAwareBitConverter.ToInt32(packetHeaderResult.Buffer.Span.Slice(0, sizeof(int)));

            if (packetSize == 0)
            {
                return (NetworkPacket.Deserialise(packetHeaderResult.Buffer), packetHeaderResult);
            }

            TransmissionResult packetDataResult =
                await ReadFromAsync(socket, packetSize, packetHeaderResult.RemoteEndPoint, socketFlags, cancellationToken);

            byte[] serialisedPacket = new byte[NetworkPacket.HeaderSize + packetSize];

            Memory<byte> serialisedPacketHeader = new Memory<byte>(serialisedPacket, 0, NetworkPacket.HeaderSize);
            packetHeaderResult.Buffer.CopyTo(serialisedPacketHeader);

            Memory<byte> serialisedPacketData = new Memory<byte>(serialisedPacket, NetworkPacket.HeaderSize, packetSize);
            packetDataResult.Buffer.CopyTo(serialisedPacketData);

            return (NetworkPacket.Deserialise(serialisedPacket), packetDataResult);
            */
        }

        /// <summary>
        /// Writes the given packet to the network, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="serialisedPacket">The packet that should be written to the network.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        /// <param name="cancellationToken">The cancellation token that should be observed for the duration of the task.</param>
        internal static async Task WritePacketAsync(Socket socket, SerialisedPacket serialisedPacket, SocketFlags socketFlags,
            CancellationToken cancellationToken = default)
        {
            int packetCount = serialisedPacket.Contents.Length / NetworkPacket.PacketSize;

            for (int i = 0; i < packetCount; i++)
            {
                ReadOnlyMemory<byte> packetDataSegment =
                    serialisedPacket.Contents.Slice(NetworkPacket.PacketSize * i, NetworkPacket.PacketSize);

                NetworkPacket packet = new NetworkPacket(packetDataSegment, packetDataSegment.Length,
                    serialisedPacket.Type, NetworkErrorCode.Ok, true);

                await WriteAsync(socket, NetworkPacket.Serialise(packet), socketFlags, cancellationToken);
            }

            if (serialisedPacket.Contents.Length % NetworkPacket.PacketSize != 0)
            {
                ReadOnlyMemory<byte> packetDataSegment =
                    serialisedPacket.Contents.Slice(NetworkPacket.PacketSize * packetCount, serialisedPacket.Contents.Length % NetworkPacket.PacketSize);

                NetworkPacket packet = new NetworkPacket(packetDataSegment, packetDataSegment.Length,
                    serialisedPacket.Type, NetworkErrorCode.Ok, true);

                await WriteAsync(socket, NetworkPacket.Serialise(packet), socketFlags, cancellationToken);
            }
        }

        /// <summary>
        /// Writes the given packet to the given remote endpoint, via the given socket.
        /// The given <see cref="SocketFlags"/> are associated with the write.
        /// </summary>
        /// <param name="socket">The socket which should write data to the network.</param>
        /// <param name="remoteEndPoint">The remote endpoint to which data should be written.</param>
        /// <param name="serialisedPacket">The packet that should be written to the remote endpoint.</param>
        /// <param name="socketFlags">The socket flags associated with the send operation.</param>
        /// <param name="cancellationToken">The cancellation token that should be observed for the duration of the task.</param>
        internal static async Task WritePacketToAsync(Socket socket, EndPoint remoteEndPoint, SerialisedPacket serialisedPacket, SocketFlags socketFlags,
            CancellationToken cancellationToken = default)
        {
            int packetCount = serialisedPacket.Contents.Length / NetworkPacket.PacketSize;

            for (int i = 0; i < packetCount; i++)
            {
                ReadOnlyMemory<byte> packetDataSegment =
                    serialisedPacket.Contents.Slice(NetworkPacket.PacketSize * i, NetworkPacket.PacketSize);

                NetworkPacket packet = new NetworkPacket(packetDataSegment, packetDataSegment.Length,
                    serialisedPacket.Type, NetworkErrorCode.Ok, true);

                await WriteToAsync(socket, remoteEndPoint, NetworkPacket.Serialise(packet), socketFlags, cancellationToken);
            }

            if (serialisedPacket.Contents.Length % NetworkPacket.PacketSize != 0)
            {
                ReadOnlyMemory<byte> packetDataSegment =
                    serialisedPacket.Contents.Slice(NetworkPacket.PacketSize * packetCount, serialisedPacket.Contents.Length % NetworkPacket.PacketSize);

                NetworkPacket packet = new NetworkPacket(packetDataSegment, packetDataSegment.Length,
                    serialisedPacket.Type, NetworkErrorCode.Ok, true);

                await WriteToAsync(socket, remoteEndPoint, NetworkPacket.Serialise(packet), socketFlags, cancellationToken);
            }
        }
    }
}