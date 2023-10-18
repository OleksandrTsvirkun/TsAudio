namespace TsAudio.Formats.Wav;

public interface IWavFormatMetadataReader
{
    ValueTask<WavMetadata> ReadWavFormatMetadataAsync(Stream stream, CancellationToken cancellationToken = default);
}
