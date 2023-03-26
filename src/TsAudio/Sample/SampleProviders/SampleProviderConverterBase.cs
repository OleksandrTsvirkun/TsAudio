using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Helper base class for classes converting to ISampleProvider
    /// </summary>
    public abstract class SampleProviderConverterBase<T> : ISampleProvider
        where T : struct
    {
        protected readonly int bytesPerSample;

        /// <summary>
        /// Source Wave Provider
        /// </summary>
        protected IWaveProvider waveProvider;

        /// <summary>
        /// Wave format of this wave provider
        /// </summary>
        public WaveFormat WaveFormat { get; }

        private MemoryPool<byte> pool;
        public MemoryPool<byte> Pool
        {
            get => this.pool ??= MemoryPool<byte>.Shared;
            set => this.pool = value ?? MemoryPool<byte>.Shared;
        }

        /// <summary>
        /// Initialises a new instance of SampleProviderConverterBase
        /// </summary>
        /// <param name="source">Source Wave provider</param>
        public SampleProviderConverterBase(IWaveProvider source, int bytesPerSample)
        {
            this.waveProvider = source;
            this.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, source.WaveFormat.Channels);
            this.bytesPerSample = bytesPerSample;
        }

        /// <summary>
        /// Reads samples from the source wave provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <returns>Number of samples read</returns>
        public async ValueTask<int> ReadAsync(Memory<float> buffer, CancellationToken cancellationToken = default)
        {
            var sourceBytesRequired = buffer.Length * this.bytesPerSample;
            using var sourceBufferOwner = this.Pool.Rent(sourceBytesRequired);
            var sourceBuffer = sourceBufferOwner.Memory.Slice(0, sourceBytesRequired);
            int bytesRead = await this.waveProvider.ReadAsync(sourceBuffer, cancellationToken);
            sourceBuffer = sourceBuffer.Slice(0, bytesRead);

            this.TransformSamples(buffer.Span, this.bytesPerSample, bytesRead, sourceBuffer.Span);

            return bytesRead / this.bytesPerSample;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void TransformSamples(Span<float> buffer, int size, int bytesRead, ReadOnlySpan<byte> sourceBuffer)
        {
            var samples = MemoryMarshal.Cast<byte, T>(sourceBuffer);

            for(int n = 0, outIndex = 0; n < bytesRead; n += size)
            {
                this.TransformSample(buffer, samples, n, ref outIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void TransformSample(Span<float> buffer, ReadOnlySpan<T> sourceBuffer, int n, ref int outIndex);
    }
}
