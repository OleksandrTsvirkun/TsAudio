namespace TsAudio.Wav.Wave.WaveProvider;

public interface IWavFormatMetadataReader
{
    Task<WavMetadata> ReadWavFormatMetadataAsync(Stream stream, CancellationToken cancellationToken = default);
}
