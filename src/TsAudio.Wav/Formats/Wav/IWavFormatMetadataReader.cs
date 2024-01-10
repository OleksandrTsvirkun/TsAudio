using TsAudio.Utils.Streams;

namespace TsAudio.Formats.Wav;

public interface IWavFormatMetadataReader
{
    ValueTask<WavMetadata> ReadWavFormatMetadataAsync(IStreamReader stream, CancellationToken cancellationToken = default);
}
