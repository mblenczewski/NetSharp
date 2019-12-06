using System;
using System.Net.Sockets;
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
        public TcpClient() : base(SocketType.Stream, ProtocolType.Tcp, SocketOptionManager.Tcp)
        {
        }

        /// <inheritdoc />
        public override async Task SendBytesAsync(byte[] buffer, TimeSpan timeout)
        {
            SimpleDataPacket packet = new SimpleDataPacket(buffer);
            await SendSimpleAsync(packet, timeout);
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
            ReadOnlyMemory<byte> serialisedRequest = request.Serialise();
            Packet rawRequest = new Packet(serialisedRequest, packetTypeId, NetworkErrorCode.Ok);
            await DoSendPacketAsync(socket, rawRequest, SocketFlags.None, timeout);

            Packet rawResponsePacket = await DoReceivePacketAsync(socket, SocketFlags.None, timeout);
            Rep responsePacket = new Rep();
            responsePacket.Deserialise(rawResponsePacket.Buffer);
            responsePacket.AfterDeserialisation();

            return responsePacket;
        }

        /// <inheritdoc />
        public override async Task SendSimpleAsync<Req>(Req request, TimeSpan timeout)
        {
            uint packetTypeId = PacketRegistry.GetPacketId<Req>();

            request.BeforeSerialisation();
            ReadOnlyMemory<byte> serialisedRequest = request.Serialise();
            Packet rawRequest = new Packet(serialisedRequest, packetTypeId, NetworkErrorCode.Ok);
            await DoSendPacketAsync(socket, rawRequest, SocketFlags.None, timeout);
        }
    }
}