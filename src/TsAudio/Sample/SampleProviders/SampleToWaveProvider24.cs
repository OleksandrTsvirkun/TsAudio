using System;

using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts a sample provider to 24 bit PCM, optionally clipping and adjusting volume along the way
    /// </summary>
    public class SampleToWaveProvider24 : SampleToWaveProviderBase
    {
        /// <summary>
        /// The Format of this IWaveProvider
        /// <see cref="IWaveProvider.WaveFormat"/>
        /// </summary>
        public override WaveFormat WaveFormat { get; }

        /// <summary>
        /// Converts from an ISampleProvider (IEEE float) to a 16 bit PCM IWaveProvider.
        /// Number of channels and sample rate remain unchanged.
        /// </summary>
        /// <param name="sourceProvider">The input source provider</param>
        public SampleToWaveProvider24(ISampleProvider sourceProvider) : base(sourceProvider, 3)
        {
            this.WaveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 24, sourceProvider.WaveFormat.Channels);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer)
        {
            int destOffset = 0;
            for(int sample = 0; sample < sampleBuffer.Length; sample++)
            {
                // adjust volume
                float sample32 = sampleBuffer[sample];

                var sample24 = (int)(sample32 * 8388607.0);
                buffer[destOffset++] = (byte)(sample24);
                buffer[destOffset++] = (byte)(sample24 >> 8);
                buffer[destOffset++] = (byte)(sample24 >> 16);
            }
        }
    }
}
