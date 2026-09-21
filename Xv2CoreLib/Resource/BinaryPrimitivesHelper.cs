using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Xv2CoreLib.Resource
{
    //Provides methods for reading and writing floating point values with BinaryPrimitives that supports both newer .NET as well as .NET Framework 4.8 - which lacks methods for writing floats
    public static class BinaryPrimitivesHelper
    {
        public static float ReadHalfLittleEndianAsFloat(Span<byte> span)
        {
#if NET5_0_OR_GREATER
            return (float)BinaryPrimitives.ReadHalfLittleEndian(span);
#else

            if (!BitConverter.IsLittleEndian)
            {
                span.Slice(0, 2).Reverse();
            }

            ushort bits = MemoryMarshal.Read<ushort>(span);
            return (float)Half.ToHalf(bits);
#endif
        }

        public static float ReadSingleLittleEndian(ReadOnlySpan<byte> span)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(span);
#else
            return MemoryMarshal.Read<float>(span);
#endif
        }

        public static float ReadSingleLittleEndian(Span<byte> span)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(span);
#else

            if (!BitConverter.IsLittleEndian)
            {
                span.Slice(0, sizeof(float)).Reverse();
            }

            return MemoryMarshal.Read<float>(span);
#endif
        }

        public static double ReadDoubleLittleEndian(Span<byte> span)
        {
#if NET5_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(span);
#else

            if (!BitConverter.IsLittleEndian)
            {
                span.Slice(0, sizeof(double)).Reverse();
            }

            return MemoryMarshal.Read<double>(span);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteHalfLittleEndian(Span<byte> span, float value)
        {
            //Taking in a float and casting it to a half is intentional.
            //Some XV2 file types use the half type (EMD, EMA, EAN), but we never actually store the value in memory as a half - it is always a 32 bit float

#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteHalfLittleEndian(span, (Half)value);
#else
            BinaryPrimitives.WriteUInt16LittleEndian(span, Half.GetBits((Half)value));
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteSingleLittleEndian(Span<byte> span, float value)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteSingleLittleEndian(span, value);
#else
            MemoryMarshal.Write(span, ref value);

            if (!BitConverter.IsLittleEndian)
            {
                span.Slice(0, sizeof(float)).Reverse();
            }
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteDoubleLittleEndian(Span<byte> span, double value)
        {
#if NET5_0_OR_GREATER
            BinaryPrimitives.WriteDoubleLittleEndian(span, value);
#else
            MemoryMarshal.Write(span, ref value);

            if (!BitConverter.IsLittleEndian)
            {
                span.Slice(0, (sizeof(double))).Reverse();
            }
#endif
        }
    }
}
