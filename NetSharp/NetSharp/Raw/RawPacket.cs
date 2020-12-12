using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NetSharp.Raw
{
    internal static class RawPacket
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Serialise(
            in Memory<byte> backingBuffer,
            in RawPacketHeader packetHeader,
            in ReadOnlyMemory<byte> packetData)
        {
            Debug.Assert(
                backingBuffer.Length >= RawPacketHeader.Length + packetData.Length,
                "Attempted to serialise packet to an undersized buffer!");

            packetHeader.Serialise(backingBuffer.Span.Slice(0, RawPacketHeader.Length));

            packetData.CopyTo(backingBuffer.Slice(RawPacketHeader.Length, packetData.Length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int TotalSize(in RawPacketHeader packetHeader)
        {
            return RawPacketHeader.Length + packetHeader.DataLength;
        }
    }
}
