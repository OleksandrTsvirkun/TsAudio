namespace TsAudio.Formats.Wav;

public interface IWavFormatMetadataReader
{
    Task<WavMetadata> ReadWavFormatMetadataAsync(Stream stream, CancellationToken cancellationToken = default);
}
