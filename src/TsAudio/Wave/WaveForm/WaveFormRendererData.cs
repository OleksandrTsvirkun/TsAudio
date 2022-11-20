using TsAudio.Sample.PeekProviders;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveForm
{
    public class WaveFormRendererData
    {
        public IWaveStream WaveStream { get; init; }

        public WaveStreamMetadata Metadata { get; init; }

        public IPeakProvider PeakProvider { get; set; }
    }
}
