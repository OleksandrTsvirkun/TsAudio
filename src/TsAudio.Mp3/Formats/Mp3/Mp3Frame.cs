using System;
using System.Buffers;

namespace TsAudio.Formats.Mp3;

public class Mp3Frame : Mp3FrameHeader, IDisposable, IEquatable<Mp3Frame>
{
    /// <summary>
    /// Raw frame data (includes header bytes)
    /// </summary>
    public IMemoryOwner<byte> RawData { get; internal set; }

    public void Dispose()
    {
        this.RawData?.Dispose();
    }

    public override int GetHashCode()
    {
        var headerHash = base.GetHashCode();
        var hashCode = new HashCode();
        hashCode.Add(headerHash);

        if (this.RawData is not null)
        {
            hashCode.AddBytes(this.RawData.Memory.Span);
        }

        return hashCode.ToHashCode();
    }

    public override bool Equals(object? obj)
    {
        return this.Equals(obj as Mp3Frame);
    }

    public bool Equals(Mp3Frame? other)
    {
        return base.Equals(other) 
            && this.RawData is not null 
            && other.RawData is not null
            && this.RawData.Memory.Span.SequenceEqual(other.RawData.Memory.Span);
    }
}
