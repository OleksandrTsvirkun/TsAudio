using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Helper class for when you need to convert back to an IWaveProvider from
    /// an ISampleProvider. Keeps it as IEEE float
    /// </summary>
    public class SampleToWaveProvider : SampleToWaveProviderBase
    {
        public override WaveFormat WaveFormat => this.sampleProvider.WaveFormat;
        /// <summary>
        /// Initializes a new instance of the WaveProviderFloatToWaveProvider class
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public SampleToWaveProvider(ISampleProvider sourceProvider) : base(sourceProvider, 4)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer)
        {
            MemoryMarshal.Cast<float, byte>(sampleBuffer).CopyTo(buffer);
        }
    }
}
