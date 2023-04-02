using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveStreams;

public abstract class WaveStream : IWaveStream, IDisposable
{
    public abstract WaveFormat WaveFormat { get; }

    public abstract long? TotalSamples { get; }

    public abstract long Position { get; }

    public abstract ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default);

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

    protected virtual ValueTask DisposeAsyncCore()
    {
        return default;
    }

    protected virtual void Dispose(bool disposing)
    {

    }

    ~WaveStream()
    {
        this.Dispose(false);
    }
}
