using TsAudio.Utils.Threading;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wav.Wave.WaveProvider;
public class WavWaveStream : WaveStream
{
    private readonly Stream stream;
    private readonly IWavFormatMetadataReader metadataReader;
    private readonly SemaphoreSlim repositionLock = new(1, 1);

    private WavMetadata metadata;

    public override WaveFormat WaveFormat => this.metadata?.WaveFormat ?? throw new InvalidOperationException("Must call init first.");

    public override long? TotalSamples
    {
        get
        {
            if(this.metadata is null)
            {
                throw new InvalidOperationException("Must call init first.");
            }

            return this.metadata.DataChuckLength / ((this.WaveFormat.BitsPerSample/ 8) * this.WaveFormat.Channels);
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

            return (this.stream.Position - this.metadata.DataChuckPosition) / ((this.WaveFormat.BitsPerSample / 8) * this.WaveFormat.Channels);
        }
    }

    public WavWaveStream(Stream stream, IWavFormatMetadataReader? metadataReader = null)
    {
        this.stream = stream;
        this.metadataReader = metadataReader ?? WavFormatMetadataReader.Instance;
    }

    public override async ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        this.metadata = await this.metadataReader.ReadWavFormatMetadataAsync(this.stream, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.stream.ReadAsync(buffer, cancellationToken);
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken);

        this.stream.Position = this.metadata.DataChuckPosition + position * ((this.WaveFormat.BitsPerSample / 8) * this.WaveFormat.Channels);
    }
}
