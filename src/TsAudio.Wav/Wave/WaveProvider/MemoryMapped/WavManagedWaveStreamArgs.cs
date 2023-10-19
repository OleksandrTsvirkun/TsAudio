using TsAudio.Formats.Wav;

namespace TsAudio.Wave.WaveProvider.MemoryMapped;

internal class WavManagedWaveStreamArgs
{
    public required WavMetadata Metadata {  get; init; }

    public required Stream Reader { get; init; }
}
