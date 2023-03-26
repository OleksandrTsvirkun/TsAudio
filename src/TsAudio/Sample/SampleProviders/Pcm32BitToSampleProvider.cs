using System;
using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts an IWaveProvider containing 32 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class Pcm32BitToSampleProvider : SampleProviderConverterBase<byte>
    {
        /// <summary>
        /// Initialises a new instance of Pcm32BitToSampleProvider
        /// </summary>
        /// <param name="source">Source Wave Provider</param>
        public Pcm32BitToSampleProvider(IWaveProvider source) : base(source, 4)
        {

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSample(Span<float> buffer, ReadOnlySpan<byte> sourceBuffer, int n, ref int outIndex)
        {
            buffer[outIndex++] = (((sbyte)sourceBuffer[n + 3] << 24 
                                    | sourceBuffer[n + 2] << 16) 
                                    | (sourceBuffer[n + 1] << 8) 
                                    | sourceBuffer[n]) / 2147483648f;
        }
    }
}
