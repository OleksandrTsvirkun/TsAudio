using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts an IWaveProvider containing 8 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class Pcm8BitToSampleProvider : SampleProviderConverterBase<byte>
    {
        private const int BytesPerSample = 1;

        /// <summary>
        /// Initialises a new instance of Pcm8BitToSampleProvider
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public Pcm8BitToSampleProvider(IWaveProvider source) : base(source)
        {
        }

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <returns>Number of samples read</returns>
        public override ValueTask<int> ReadAsync(Memory<float> buffer, CancellationToken cancellationToken = default)
        {
            return this.ReadAsync(buffer, BytesPerSample, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSample(Span<float> buffer, ReadOnlySpan<byte> sourceBuffer, int n, ref int outIndex)
        {
            buffer[outIndex++] = sourceBuffer[n] / 128f - 1.0f;
        }
    }
}
