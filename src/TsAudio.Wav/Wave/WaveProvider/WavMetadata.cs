using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wav.Wave.WaveProvider;

public class WavMetadata
{
    public WaveFormat WaveFormat { get; init; }

    public long DataChuckPosition { get; init; }

    public long DataChuckLength { get; init; }

    public bool isRf64 { get; init; }

    public IReadOnlyCollection<RiffChunk> RiffChunks { get; init; }
}
