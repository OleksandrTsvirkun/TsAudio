using System;
using System.Buffers;
using System.Collections;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;

namespace TsAudio.Sample.PeekProviders
{
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

        public PeakInfo Current { get; protected set; }

        public void Init(ISampleProvider provider, int samplesPerPeak)
        {
            this.SamplesPerPeak = samplesPerPeak;
            this.Provider = provider;
            this.SamplesPerPeak = samplesPerPeak;

            this.BufferOwner?.Dispose();
            this.BufferOwner = this.Pool.Rent(samplesPerPeak);
        }

        public abstract ValueTask<bool> MoveNextAsync();

        public ValueTask DisposeAsync()
        {
            this.BufferOwner.Dispose();
            return default;
        }
    }
}
