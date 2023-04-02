using TsAudio.Sample.PeekProviders;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveForm;

public class WaveFormRendererData
{
    public IWaveStream WaveStream { get; init; }

    public long? TotalSamples { get; init; }

    public IPeakProvider PeakProvider { get; set; }
}
