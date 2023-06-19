using TsAudio.Utils.Threading;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wav.Wave.WaveProvider;

public class WavManagedWaveStream : WaveStream
{
    private readonly Stream stream;
    private readonly long dataChunkPosition;
    private readonly long dataChunkLength;
    private readonly SemaphoreSlim repositionLock = new(1, 1);

    public override WaveFormat WaveFormat { get; }

    public override long? TotalSamples { get; }

    public override long Position => (this.stream.Position - this.dataChunkPosition) / ((this.WaveFormat.BitsPerSample / 8) * this.WaveFormat.Channels);

    internal WavManagedWaveStream(WavManagedWaveStreamArgs args)
    {
        this.stream = args.Reader;
        this.dataChunkLength = args.DataChuckLength;
        this.dataChunkPosition = args.DataChuckPosition;
        this.WaveFormat = args.WaveFormat;
        this.TotalSamples = this.dataChunkLength / ((this.WaveFormat.BitsPerSample / 8) * this.WaveFormat.Channels);
    }

    public override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        this.stream.Seek(this.dataChunkPosition, SeekOrigin.Begin); 
        return ValueTask.CompletedTask;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.stream.ReadAsync(buffer, cancellationToken);
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken);

        this.stream.Position = this.dataChunkPosition + position * ((this.WaveFormat.BitsPerSample / 8) * this.WaveFormat.Channels);
    }
}
