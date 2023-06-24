using TsAudio.Wav.Formats.Wav;

namespace TsAudio.Wav.Wave.WaveProvider.MemoryMapped;
public class WavFileWaveStream : WavWaveStream
{
    protected readonly IWavFormatMetadataReader metadataReader;

    public WavFileWaveStream(Stream stream, IWavFormatMetadataReader? metadataReader = null) : base(stream)
    {
        this.metadataReader = metadataReader ?? WavFormatMetadataReader.Instance;
    }

    public override async ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        this.metadata = await this.metadataReader.ReadWavFormatMetadataAsync(this.stream, cancellationToken);
        this.stream.Seek(this.metadata.DataChunkPosition, SeekOrigin.Begin);
    }
}
