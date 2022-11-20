using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;

using TsAudio.Utils;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts an IWaveProvider containing 32 bit PCM to an
    /// ISampleProvider
    /// </summary>
    public class Pcm32BitToSampleProvider : SampleProviderConverterBase<byte>
    {
        private const int BytesPerSample = 4;

        /// <summary>
        /// Initialises a new instance of Pcm32BitToSampleProvider
        /// </summary>
        /// <param name="source">Source Wave Provider</param>
        public Pcm32BitToSampleProvider(IWaveProvider source) : base(source)
        {

        }

        /// <summary>
        /// Reads floating point samples from this sample provider
        /// </summary>
        /// <param name="buffer">sample buffer</param>
        /// <returns>number of samples provided</returns>
        public override ValueTask<int> ReadAsync(Memory<float> buffer, CancellationToken cancellationToken = default)
        {
            return this.ReadAsync(buffer, BytesPerSample, cancellationToken);
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
