using System;
using System.IO;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveStreams;
public abstract class WaveStream : Stream, IWaveStream
{
    public abstract WaveFormat WaveFormat { get; }

    public sealed override bool CanWrite => false;

    public override bool CanRead => true;

    public virtual int BlockAlign => this.WaveFormat.BlockAlign;

    public virtual TimeSpan TotalTime => TimeSpan.FromSeconds((double)this.Length / this.WaveFormat.AverageBytesPerSecond);

    public virtual TimeSpan CurrentTime
    {
        get => TimeSpan.FromSeconds((double)this.Position / this.WaveFormat.AverageBytesPerSecond);
        set => this.Position = (long)(value.TotalSeconds * this.WaveFormat.AverageBytesPerSecond);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return this.Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.Position + offset,
            SeekOrigin.End => this.Length - offset,
            _ => throw new NotSupportedException()
        };
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public sealed override void Flush() { }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Can't write to a WaveStream");
    }

    public sealed override void SetLength(long length)
    {
        throw new NotSupportedException("Can't set length of a WaveStream");
    }
}
