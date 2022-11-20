using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    public class SampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sampleProvider;

        public WaveFormat WaveFormat => this.sampleProvider.WaveFormat;

        public SampleProvider(IWaveProvider waveProvider, bool forceStereo = false)
        {
            this.sampleProvider = SampleProviderConverters.ConvertWaveProviderIntoSampleProvider(waveProvider);
        }

        public ValueTask<int> ReadAsync(Memory<float> buffer, CancellationToken cancellationToken)
        {
            return this.sampleProvider.ReadAsync(buffer, cancellationToken);
        }
    }
}
