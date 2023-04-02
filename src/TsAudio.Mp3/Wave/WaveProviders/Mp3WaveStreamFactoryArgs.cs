using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveProviders;

public class Mp3WaveStreamFactoryArgs
{
    public IStreamManager StreamManager { get; set; }

    public long? TotalSamples { get; set; }
}

