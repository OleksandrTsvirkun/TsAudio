using TsAudio.Formats.Wav;
using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveProvider.MemoryMapped;

internal class WavManagedWaveStreamArgs
{
    public required WavMetadata Metadata {  get; init; }

    public required IStreamReader Reader { get; init; }
}
