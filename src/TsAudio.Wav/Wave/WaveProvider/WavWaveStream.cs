using TsAudio.Utils.Threading;
using TsAudio.Wav.Formats.Wav;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wav.Wave.WaveProvider.MemoryMapped;
public abstract class WavWaveStream : WaveStream
{
    protected readonly Stream stream;
    protected readonly SemaphoreSlim repositionLock = new(1, 1);

    protected WavMetadata metadata;

    public override WaveFormat WaveFormat => this.metadata?.WaveFormat ?? throw new InvalidOperationException("Must call init first.");

    public override long? TotalSamples
    {
        get
        {
            if(this.metadata is null)
            {
                throw new InvalidOperationException("Must call init first.");
            }

            return this.metadata.DataChunkLength / (this.WaveFormat.BitsPerSample / 8 * this.WaveFormat.Channels);
        }
    }

    public override long Position
    {
        get
        {
            if(this.metadata is null)
            {
                return 0;
            }

            return (this.stream.Position - this.metadata.DataChunkPosition) / (this.WaveFormat.BitsPerSample / 8 * this.WaveFormat.Channels);
        }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public WavWaveStream(Stream stream, IWavFormatMetadataReader? metadataReader = null)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        this.stream = stream;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.stream.ReadAsync(buffer, cancellationToken);
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken);

        this.stream.Position = this.metadata.DataChunkPosition + position * (this.WaveFormat.BitsPerSample / 8 * this.WaveFormat.Channels);
    }
}
