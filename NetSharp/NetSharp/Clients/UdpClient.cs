using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;
using NetSharp.Servers;
using NetSharp.Utils;
using NetSharp.Utils.Socket_Options;

namespace NetSharp.Clients
{
    /// <summary>
    /// Provides methods for UDP communication with a connected <see cref="UdpServer"/> instance.
    /// </summary>
    public sealed class UdpClient : Client
    {
        /// <inheritdoc />
        public UdpClient() : base(SocketType.Dgram, ProtocolType.Udp, SocketOptionManager.Udp)
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
            await DoSendPacketToAsync(socket, remoteEndPoint, rawRequest, SocketFlags.None, timeout);

            (Packet rawResponsePacket, TransmissionResult packetResult) =
                await DoReceivePacketFromAsync(socket, remoteEndPoint, SocketFlags.None, timeout);
            remoteEndPoint = packetResult.RemoteEndPoint;

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
            await DoSendPacketToAsync(socket, remoteEndPoint, rawRequest, SocketFlags.None, timeout);
        }
    }
}