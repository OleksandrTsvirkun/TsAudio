using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveStreams;
public abstract class WaveStream : IWaveStream
{
    public abstract WaveFormat WaveFormat { get; }

    public abstract long Length { get; }

    public abstract long Position { get; }

    public virtual int BlockAlign => this.WaveFormat.BlockAlign;

    public virtual TimeSpan TotalTime => this.PositionToTime(this.Length);

    public virtual TimeSpan CurrentTime => this.PositionToTime(this.Position);

    public abstract ValueTask ChangePositionAsync(long position, CancellationToken cancellationToken = default);

    public abstract ValueTask ChangePositionAsync(TimeSpan time, CancellationToken cancellationToken = default);

    public abstract ValueTask InitAsync(CancellationToken cancellationToken = default);

    public abstract ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await this.DisposeAsyncCore().ConfigureAwait(false);
        this.Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected abstract ValueTask DisposeAsyncCore();

    protected abstract void Dispose(bool disposing);

    protected TimeSpan PositionToTime(long position)
    {
        return TimeSpan.FromSeconds((double)position / this.WaveFormat.AverageBytesPerSecond);
    }

    protected long TimeToPosition(TimeSpan time)
    {
        return (long)(time.TotalSeconds * this.WaveFormat.AverageBytesPerSecond);
    }

    ~WaveStream()
    {
        this.Dispose(false);
    }
}
