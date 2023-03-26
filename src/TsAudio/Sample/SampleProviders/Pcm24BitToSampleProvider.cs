using System;
using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts an IWaveProvider containing 24 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class Pcm24BitToSampleProvider : SampleProviderConverterBase<byte>
    {
        /// <summary>
        /// Initialises a new instance of Pcm24BitToSampleProvider
        /// </summary>
        /// <param name="source">Source Wave Provider</param>
        public Pcm24BitToSampleProvider(IWaveProvider source) : base(source, 3)
        { 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSample(Span<float> buffer, ReadOnlySpan<byte> sourceBuffer, int n, ref int outIndex)
        {
            buffer[outIndex++] = (((sbyte)sourceBuffer[n + 2] << 16)
                                         | (sourceBuffer[n + 1] << 8)
                                         | sourceBuffer[n])
                                         / 8388608f;
        }
    }
}
