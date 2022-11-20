using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Helper class for when you need to convert back to an IWaveProvider from
    /// an ISampleProvider. Keeps it as IEEE float
    /// </summary>
    public class SampleToWaveProvider : IWaveProvider
    {
        private readonly ISampleProvider sampleProvider;

        /// <summary>
        /// The waveformat of this WaveProvider (same as the source)
        /// </summary>
        public WaveFormat WaveFormat => this.sampleProvider.WaveFormat;

        private MemoryPool<float> pool;
        public MemoryPool<float> Pool
        {
            get => this.pool ??= MemoryPool<float>.Shared;
            set => this.pool = value ?? MemoryPool<float>.Shared;
        }

        /// <summary>
        /// Initializes a new instance of the WaveProviderFloatToWaveProvider class
        /// </summary>
        /// <param name="source">Source wave provider</param>
        public SampleToWaveProvider(ISampleProvider source)
        {
            if(source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Must be already floating point");
            }

            this.sampleProvider = source;
        }

        /// <summary>
        /// Reads from this provider
        /// </summary>
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int samplesRequired = buffer.Length / sizeof(float);

            using var sourceBufferOwner = this.Pool.Rent(samplesRequired);

            var sourceBuffer = sourceBufferOwner.Memory.Slice(0, samplesRequired);

            var sourceSamples = await this.sampleProvider.ReadAsync(sourceBuffer, cancellationToken);

            this.TransformSamples(buffer.Span.Slice(0, sourceSamples), sourceBuffer.Span.Slice(0, sourceSamples));

            return sourceSamples * sizeof(float);
        }

        protected virtual void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer)
        {
            MemoryMarshal.Cast<float, byte>(sampleBuffer).CopyTo(buffer);
        }
    }
}
