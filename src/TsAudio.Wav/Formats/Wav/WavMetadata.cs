using TsAudio.Wave.WaveFormats;

namespace TsAudio.Formats.Wav;

public class WavMetadata
{
    public WaveFormat WaveFormat { get; init; }

    public long DataChunkPosition { get; init; }

    public long DataChunkLength { get; init; }

    public bool IsRf64 { get; init; }

    public IReadOnlyCollection<RiffChunk> RiffChunks { get; init; }
}
