using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;
using NetSharp.Servers;
using NetSharp.Utils.Socket_Options;

namespace NetSharp.Clients
{
    /// <summary>
    /// Provides methods for TCP communication with a connected <see cref="TcpServer"/> instance.
    /// </summary>
    public sealed class TcpClient : Client
    {
        /// <inheritdoc />
        public TcpClient() : base(SocketType.Stream, ProtocolType.Tcp, new TcpSocketOptions(ref socket))
        {
        }

        /// <inheritdoc />
        public override async Task<bool> SendBytesAsync(byte[] buffer, TimeSpan timeout)
        {
            SimpleDataPacket packet = new SimpleDataPacket(buffer);
            return await SendSimpleAsync(packet, timeout);
        }

        /// <inheritdoc />
        public override async Task<byte[]> SendBytesWithResponseAsync(byte[] buffer, TimeSpan timeout)
        {
            DataPacket packet = new DataPacket(buffer);
            DataResponsePacket response = await SendComplexAsync<DataPacket, DataResponsePacket>(packet, timeout);

            return response.ResponseBuffer.ToArray();
        }

        /// <inheritdoc />
        public override async Task<Rep> SendComplexAsync<Req, Rep>(Req request, TimeSpan timeout)
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();

            request.BeforeSerialisation();
            Memory<byte> serialisedRequest = request.Serialise();
            SerialisedPacket rawRequest = new SerialisedPacket(serialisedRequest, packetTypeId);
            await DoSendPacketAsync(socket, rawRequest, SocketFlags.None, timeout);

            SerialisedPacket rawResponsePacket = await DoReceivePacketAsync(socket, SocketFlags.None, timeout);
            Rep responsePacket = new Rep();
            responsePacket.Deserialise(rawResponsePacket.Contents);
            responsePacket.AfterDeserialisation();

            return responsePacket;
        }

        /// <inheritdoc />
        public override async Task<bool> SendSimpleAsync<Req>(Req request, TimeSpan timeout)
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();

            request.BeforeSerialisation();
            Memory<byte> serialisedRequest = request.Serialise();
            SerialisedPacket rawRequest = new SerialisedPacket(serialisedRequest, packetTypeId);
            return await DoSendPacketAsync(socket, rawRequest, SocketFlags.None, timeout);
        }
    }
}