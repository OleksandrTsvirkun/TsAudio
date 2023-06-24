using TsAudio.Wav.Formats.Wav;

namespace TsAudio.Wav.Wave.WaveProvider.MemoryMapped;

internal class WavManagedWaveStreamArgs
{
    public WavMetadata Metadata {  get; init; }

    public Stream Reader { get; init; }
}
