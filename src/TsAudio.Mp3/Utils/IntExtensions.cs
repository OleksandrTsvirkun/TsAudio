using System;

namespace TsAudio.Utils;

public static class IntExtensions
{
    public static int ToBigEndianInt32(this Span<byte> span)
    {
        Span<byte> copy = stackalloc byte[4];
        span.CopyTo(copy);
        copy.Reverse();
        return BitConverter.ToInt32(copy);
    }

    public static Span<byte> ToBigEndianSpan(this int value)
    {
        Span<byte> bytes = BitConverter.GetBytes(value);
        bytes.Reverse();
        return bytes;
    }
}
