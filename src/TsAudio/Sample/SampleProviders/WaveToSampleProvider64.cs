using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Helper class turning an already 64 bit floating point IWaveProvider
    /// into an ISampleProvider - hopefully not needed for most applications
    /// </summary>
    public class WaveToSampleProvider64 : SampleProviderConverterBase<long>
    {
        /// <summary>
        /// Initializes a new instance of the WaveToSampleProvider class
        /// </summary>
        /// <param name="source">Source wave provider, must be IEEE float</param>
        public WaveToSampleProvider64(IWaveProvider source) : base(source)
        {
            if(source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Must be already floating point");
            }
        }

        /// <summary>
        /// Reads from this provider
        /// </summary>
        public override ValueTask<int> ReadAsync(Memory<float> buffer, CancellationToken cancellationToken = default)
        {
            return ReadAsync(buffer, sizeof(long), cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void TransformSample(Span<float> buffer, ReadOnlySpan<long> sourceBuffer, int n, ref int outIndex)
        {
            buffer[outIndex++] = (float)BitConverter.Int64BitsToDouble(sourceBuffer[outIndex]);
        }
    }
}
