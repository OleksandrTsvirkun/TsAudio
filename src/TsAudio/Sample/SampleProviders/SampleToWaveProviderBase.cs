using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Helper class for when you need to convert back to an IWaveProvider from
/// an ISampleProvider. Keeps it as IEEE float
/// </summary>
public abstract class SampleToWaveProviderBase : IWaveProvider
{
    protected readonly ISampleProvider sampleProvider;
    protected readonly int bytesPerSample;

    /// <summary>
    /// The waveformat of this WaveProvider (same as the source)
    /// </summary>
    public abstract WaveFormat WaveFormat { get; }

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
    public SampleToWaveProviderBase(ISampleProvider sourceProvider, int bytesPerSample)
    {
        if(sourceProvider.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new ArgumentException("Must be already floating point");
        }

        if(sourceProvider.WaveFormat.BitsPerSample != 32)
        {
            throw new ArgumentException("Input source provider must be 32 bit", nameof(sourceProvider));
        }

        this.sampleProvider = sourceProvider;
        this.bytesPerSample = bytesPerSample;
    }
    public ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        return default;
    }

    /// <summary>
    /// Reads from this provider
    /// </summary>
    public virtual async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int samplesRequired = buffer.Length / this.bytesPerSample;

        using var sourceBufferOwner = this.Pool.Rent(samplesRequired);

        var sourceBuffer = sourceBufferOwner.Memory.Slice(0, samplesRequired);

        var sourceSamples = await this.sampleProvider.ReadAsync(sourceBuffer, cancellationToken);

        this.TransformSamples(buffer.Span, sourceBuffer.Span.Slice(0, sourceSamples));

        return sourceSamples * this.bytesPerSample;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer);
}
