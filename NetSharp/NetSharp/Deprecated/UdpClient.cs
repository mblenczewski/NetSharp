using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetSharp.Packets;
using NetSharp.Packets.Builtin;

namespace NetSharp.Deprecated
{
    /// <summary>
    /// Provides methods for UDP communication with a connected <see cref="UdpServer"/> instance.
    /// </summary>
    public sealed class UdpClient : Client
    {
        /// <inheritdoc />
        public UdpClient() : base(SocketType.Dgram, ProtocolType.Udp)
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

            bool sentPacket = false; //await DoSendPacketToAsync(socket, remoteEndPoint, rawRequest, SocketFlags.None, timeout);

            //(SerialisedPacket rawResponsePacket, EndPoint responseEndPoint) = await DoReceivePacketFromAsync(socket, remoteEndPoint, SocketFlags.None, timeout);
            //remoteEndPoint = responseEndPoint;

            Rep responsePacket = new Rep();
            //responsePacket.Deserialise(rawResponsePacket.Contents);
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

            return false; //await DoSendPacketToAsync(socket, remoteEndPoint, rawRequest, SocketFlags.None, timeout);
        }
    }
}