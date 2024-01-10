using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Helper base class for classes converting to ISampleProvider
/// </summary>
public abstract class SampleProviderConverterBase<T> : ISampleProvider, IDisposable
    where T : struct
{
    protected readonly int bytesPerSample;

    /// <summary>
    /// Source Wave Provider
    /// </summary>
    protected IWaveProvider waveProvider;
    private IMemoryOwner<byte> bufferOwner;

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
        var sourceBuffer = this.EnsureBuffer(buffer.Length * this.bytesPerSample);
        var bytesRead = await this.waveProvider.ReadAsync(sourceBuffer, cancellationToken);

        this.TransformSamples(buffer.Span, this.bytesPerSample, sourceBuffer.Span.Slice(0, bytesRead));

        return bytesRead / this.bytesPerSample;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void TransformSamples(Span<float> buffer, int size, ReadOnlySpan<byte> sourceBuffer)
    {
        var samples = MemoryMarshal.Cast<byte, T>(sourceBuffer);

        for(int n = 0, outIndex = 0; n < sourceBuffer.Length; n += size)
        {
            this.TransformSample(buffer, samples, n, ref outIndex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract void TransformSample(Span<float> buffer, ReadOnlySpan<T> sourceBuffer, int n, ref int outIndex);

    private Memory<byte> EnsureBuffer(int length)
    {
        this.bufferOwner ??= this.Pool.Rent(length);

        if(this.bufferOwner.Memory.Length < length)
        {
            this.bufferOwner.Dispose();
            this.bufferOwner = this.Pool.Rent(length);
        }

        return this.bufferOwner.Memory.Slice(0, length);
    }

    public void Dispose()
    {
        this.bufferOwner?.Dispose();
        this.bufferOwner = null;
    }
}
