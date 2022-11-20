using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Converts a sample provider to 16 bit PCM, optionally clipping and adjusting volume along the way
    /// </summary>
    public class SampleToWaveProvider16 : IWaveProvider
    {
        private readonly ISampleProvider sampleProvider;

        private MemoryPool<float> pool;
        public MemoryPool<float> Pool
        {
            get => this.pool ??= MemoryPool<float>.Shared;
            set => this.pool = value ?? MemoryPool<float>.Shared;
        }

        /// <summary>
        /// <see cref="IWaveProvider.WaveFormat"/>
        /// </summary>
        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Converts from an ISampleProvider (IEEE float) to a 16 bit PCM IWaveProvider.
        /// Number of channels and sample rate remain unchanged.
        /// </summary>
        /// <param name="sourceProvider">The input source provider</param>
        public SampleToWaveProvider16(ISampleProvider sourceProvider)
        {
            if(sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Input source provider must be IEEE float", nameof(sourceProvider));
            }


            if(sourceProvider.WaveFormat.BitsPerSample != 32)
            {
                throw new ArgumentException("Input source provider must be 32 bit", nameof(sourceProvider));
            }
                
            this.WaveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, sourceProvider.WaveFormat.Channels);

            this.sampleProvider = sourceProvider;
        }



        /// <summary>
        /// Reads bytes from this wave stream
        /// </summary>
        /// <param name="buffer">The destination buffer</param>
        /// <returns>Number of bytes read.</returns>
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int samplesRequired = buffer.Length / sizeof(short);

            using var sourceBufferOwner = Pool.Rent(samplesRequired);

            var sourceBuffer = sourceBufferOwner.Memory.Slice(0, samplesRequired);

            var sourceSamples = await sampleProvider.ReadAsync(sourceBuffer, cancellationToken);

            this.TransformSamples(buffer.Span.Slice(0, sourceSamples), sourceBuffer.Span.Slice(0, sourceSamples));

            return sourceSamples * sizeof(short);
        }

        protected virtual void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer)
        {
            var destWaveBuffer = MemoryMarshal.Cast<byte, short>(buffer);

            for(int sample = 0, destOffset = 0; sample < sampleBuffer.Length; sample++)
            {
                destWaveBuffer[destOffset++] = (short)(sampleBuffer[sample] * 32767);
            }
        }
    }
}
