using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

public class Mp3ManagedWaveStreamFactoryArgs
{
    public IStreamManager StreamManager { get; set; }

    public long? TotalSamples { get; set; }
}

