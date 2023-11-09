using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveStreams;

public abstract class WaveStream : IWaveStream
{
    public abstract WaveFormat WaveFormat { get; }

    public abstract long? TotalSamples { get; }

    public abstract long Position { get; }

    public abstract ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default);

    public abstract Task InitAsync(CancellationToken cancellationToken = default);

    public abstract ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
