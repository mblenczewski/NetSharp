using System;
using System.Runtime.CompilerServices;

namespace NetSharp.Utils.Conversion
{
    /// <summary>
    /// Wraps the <see cref="BitConverter"/> class to provide conversion that is endian-aware.
    /// </summary>
    public static class EndianAwareBitConverter
    {
        /// <summary>
        /// Reverses the given bytes if the endian-nes doesn't match.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<byte> ReverseAsNeeded(Span<byte> bytes, bool toLittleEndian)
        {
            if (toLittleEndian != BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }

            return bytes;
        }

        /// <inheritdoc cref="BitConverter.GetBytes(bool)"/>
        public static Span<byte> GetBytes(bool value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(char)"/>
        public static Span<byte> GetBytes(char value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(double)"/>
        public static Span<byte> GetBytes(double value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(float)"/>
        public static Span<byte> GetBytes(float value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(int)"/>
        public static Span<byte> GetBytes(int value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(long)"/>
        public static Span<byte> GetBytes(long value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(short)"/>
        public static Span<byte> GetBytes(short value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(uint)"/>
        public static Span<byte> GetBytes(uint value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(ulong)"/>
        public static Span<byte> GetBytes(ulong value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.GetBytes(ushort)"/>
        public static Span<byte> GetBytes(ushort value, bool littleEndian = false)
        {
            return ReverseAsNeeded(BitConverter.GetBytes(value), littleEndian);
        }

        /// <inheritdoc cref="BitConverter.ToBoolean(ReadOnlySpan{byte})"/>
        public static bool ToBoolean(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToBoolean(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToChar(ReadOnlySpan{byte})"/>
        public static char ToChar(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToChar(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToDouble(ReadOnlySpan{byte})"/>
        public static double ToDouble(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToDouble(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToInt16(ReadOnlySpan{byte})"/>
        public static short ToInt16(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToInt16(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToInt32(ReadOnlySpan{byte})"/>
        public static int ToInt32(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToInt32(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToInt64(ReadOnlySpan{byte})"/>
        public static long ToInt64(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToInt64(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToSingle(ReadOnlySpan{byte})"/>
        public static float ToSingle(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToSingle(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToUInt16(ReadOnlySpan{byte})"/>
        public static ushort ToUInt16(byte[] bytes, bool littleEndian = false)
        {
            return BitConverter.ToUInt16(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToUInt32(ReadOnlySpan{byte})"/>
        public static uint ToUInt32(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToUInt32(ReverseAsNeeded(bytes, littleEndian));
        }

        /// <inheritdoc cref="BitConverter.ToUInt64(ReadOnlySpan{byte})"/>
        public static ulong ToUInt64(Span<byte> bytes, bool littleEndian = false)
        {
            return BitConverter.ToUInt64(ReverseAsNeeded(bytes, littleEndian));
        }
    }
}