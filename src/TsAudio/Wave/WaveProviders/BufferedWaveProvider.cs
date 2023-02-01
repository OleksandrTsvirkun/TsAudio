using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders
{
    /// <summary>
    /// Provides a buffered store of samples
    /// Read method will return queued samples or fill buffer with zeroes
    /// Now backed by a circular buffer
    /// </summary>
    public class BufferedWaveProvider : IWaveProvider
    {
        protected readonly object locker = new();
        protected readonly ManualResetEventSlim asyncReadEvent = new(false);
        protected readonly ManualResetEventSlim asyncWriteEvent = new(true);

        protected IMemoryOwner<byte> memoryOwner;

        protected volatile int writePosition;
        protected volatile int readPosition;

        protected int writeGate;
        protected bool isEmpty;
        protected bool isFlushed;

        /// <summary>
        /// Gets the WaveFormat
        /// </summary>
        public WaveFormat WaveFormat { get; }

        public int Count => this.isEmpty ? 0 : this.writePosition > this.readPosition
                                        ? this.writePosition - this.readPosition
                                        : this.memoryOwner.Memory.Length - this.readPosition + this.writePosition;

        public bool AllowWait { get; set; } = true;

        public int WriteGate
        {
            get => this.writeGate;
            set => this.writeGate = Math.Max(0, Math.Min(this.memoryOwner.Memory.Length, value));
        }

        public long Length => this.memoryOwner.Memory.Length;

        public BufferedWaveProvider(WaveFormat waveFormat, int size = 4096, MemoryPool<byte> pool = null)
        {
            this.WaveFormat = waveFormat;
            this.memoryOwner = (pool ?? MemoryPool<byte>.Shared).Rent(size);
            this.WriteGate = (this.memoryOwner.Memory.Length / 4 * 3);
            this.isEmpty = true;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = 0;
            var memory = this.memoryOwner.Memory;
            while(buffer.Length > 0 && !cancellationToken.IsCancellationRequested)
            {
                if(this.Count == 0 && this.isFlushed)
                {
                    break;
                }
                else if(this.Count == 0 && this.AllowWait)
                {
                    this.asyncReadEvent.Reset();
                    this.asyncReadEvent.Wait(cancellationToken);
                    continue;
                }
                else if(this.Count == 0 && !this.AllowWait)
                {
                    break;
                }

                var writePosition = this.writePosition;
                var readPosition = this.readPosition;

                var canRead = writePosition > readPosition
                                    ? writePosition - readPosition
                                    : memory.Length - readPosition;

                var toCopy = Math.Min(canRead, buffer.Length);


                var data = memory.Slice(readPosition, toCopy);

                data.CopyTo(buffer);

                read += toCopy;

                this.readPosition += toCopy;
                this.readPosition %= this.memoryOwner.Memory.Length;

                if(this.readPosition == this.writePosition)
                {
                    this.isEmpty = true;
                }

                if(this.Length - this.Count >= this.WriteGate)
                {
                    this.asyncWriteEvent.Set();
                }

                buffer = buffer.Slice(toCopy);
            }
            return new ValueTask<int>(read);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            this.isFlushed = true;
            return default;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var memory = this.memoryOwner.Memory;
            while(buffer.Length > 0 && !cancellationToken.IsCancellationRequested)
            {
                if(this.Count >= memory.Length)
                {
                    this.asyncWriteEvent.Reset();

                    this.asyncWriteEvent.Wait(cancellationToken);

                    continue;
                }

                var writePosition = this.writePosition;
                var readPosition = this.readPosition;

                var canWrite = this.isEmpty ? memory.Length - writePosition
                                       : writePosition > readPosition
                                            ? memory.Length - writePosition
                                            : readPosition - writePosition;

                var toCopy = Math.Min(canWrite, buffer.Length);

                var data = memory.Slice(writePosition, toCopy);

                buffer.Slice(0, toCopy).CopyTo(data);

                this.writePosition += toCopy;
                this.writePosition %= memory.Length;

                if(toCopy > 0)
                {
                    this.isEmpty = false;
                }

                this.asyncReadEvent.Set();

                buffer = buffer.Slice(toCopy);
            }

            return default;
        }

        public void Clear()
        {
            lock(locker)
            {
                this.isFlushed = false;
                this.writePosition = 0;
                this.readPosition = 0;
                this.memoryOwner.Memory.Span.Clear();
                this.isEmpty = true;
                this.asyncReadEvent.Reset();
                this.asyncWriteEvent.Set();
            }
        }
    }
}
