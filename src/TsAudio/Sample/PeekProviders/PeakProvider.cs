using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;

namespace TsAudio.Sample.PeekProviders;

public abstract class PeakProvider : IPeakProvider
{
    protected ISampleProvider Provider { get; private set; }

    protected int SamplesPerPeak { get; private set; }

    protected IMemoryOwner<float> BufferOwner { get; private set; }

    private MemoryPool<float> pool;
    public MemoryPool<float> Pool
    {
        get => this.pool ??= MemoryPool<float>.Shared;
        set => this.pool = value ?? MemoryPool<float>.Shared;
    }

    public CancellationToken CancellationToken { get; set; }

    public PeakInfo Current { get; protected set; }

    public void Init(ISampleProvider provider, int samplesPerPeak)
    {
        this.SamplesPerPeak = samplesPerPeak;
        this.Provider = provider;
        this.SamplesPerPeak = samplesPerPeak;

        this.BufferOwner?.Dispose();
        this.BufferOwner = this.Pool.Rent(samplesPerPeak);
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        var buffer = this.BufferOwner.Memory.Slice(0, this.SamplesPerPeak);
        var read = await this.Provider.ReadAsync(buffer, this.CancellationToken);

        if(read == 0)
        {
            return false;
        }

        var memory = this.BufferOwner.Memory.Slice(0, read);

        this.Current = this.CalculatePeak(memory.Span);

        return true;
    }

    protected abstract PeakInfo CalculatePeak(ReadOnlySpan<float> memory);

    public async ValueTask DisposeAsync()
    {
        if(this.Provider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if(this.Provider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        this.BufferOwner?.Dispose();
    }
}
