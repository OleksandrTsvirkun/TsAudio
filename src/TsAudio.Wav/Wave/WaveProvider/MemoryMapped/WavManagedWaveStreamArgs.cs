using TsAudio.Formats.Wav;

namespace TsAudio.Wave.WaveProvider.MemoryMapped;

internal class WavManagedWaveStreamArgs
{
    public WavMetadata Metadata {  get; init; }

    public Stream Reader { get; init; }
}
