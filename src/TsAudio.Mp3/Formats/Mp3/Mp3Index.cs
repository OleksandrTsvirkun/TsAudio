using System;
using System.Diagnostics.CodeAnalysis;

namespace TsAudio.Formats.Mp3;

public struct Mp3Index : IEquatable<Mp3Index>
{
    public long StreamPosition { get; init; }

    public long SamplePosition { get; init; }

    public ushort SampleCount { get; init; }

    public ushort FrameLength { get; init; }

    public bool Equals(Mp3Index other)
    {
        return this.StreamPosition == other.StreamPosition
                && this.SamplePosition == other.SamplePosition
                && this.SampleCount == other.SampleCount
                && this.FrameLength == other.FrameLength;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is null ? false : this.Equals((Mp3Index)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(this.StreamPosition, this.SamplePosition, this.SampleCount, this.FrameLength);
    }
}
