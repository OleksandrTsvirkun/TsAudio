using System;
using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts an IWaveProvider containing 8 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class Pcm8BitToSampleProvider : SampleProviderConverterBase<byte>
    {
        /// <summary>
        /// Initialises a new instance of Pcm8BitToSampleProvider
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public Pcm8BitToSampleProvider(IWaveProvider source) : base(source, 1)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSample(Span<float> buffer, ReadOnlySpan<byte> sourceBuffer, int n, ref int outIndex)
        {
            buffer[outIndex++] = sourceBuffer[n] / 128f - 1.0f;
        }
    }
}
