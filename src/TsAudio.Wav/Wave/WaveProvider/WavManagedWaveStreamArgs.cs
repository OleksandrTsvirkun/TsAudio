using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wav.Wave.WaveProvider;

internal class WavManagedWaveStreamArgs
{
    public WaveFormat WaveFormat { get; init; }

    public long DataChuckPosition { get; init; }

    public long DataChuckLength { get; init; }

    public Stream Reader { get; init; }  
}
