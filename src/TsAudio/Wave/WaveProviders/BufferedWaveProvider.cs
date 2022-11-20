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
        protected readonly object locker = new object();

        protected IMemoryOwner<byte> memoryOwner;

        protected volatile int writePosition;
        protected volatile int readPosition;
        protected volatile bool waitForRead;
        protected volatile bool waitForWrite;

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

            using var registration = cancellationToken.Register(() =>
            {
                lock(locker)
                {
                    Monitor.PulseAll(locker);
                }
            });


            while(buffer.Length > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(this.Count == 0 && this.isFlushed)
                {
                    break;
                }
                else if(this.Count == 0 && this.AllowWait)
                {
                    this.waitForRead = true;
                    lock(locker)
                    {
                        if(this.waitForWrite)
                        {
                            Monitor.Pulse(locker);
                        }

                        Monitor.Wait(locker);
                    }
                    continue;
                }
                else if(this.Count == 0 && !this.AllowWait)
                {
                    break;
                }

                var writePosition = this.writePosition;

                var canRead = writePosition > this.readPosition
                                        ? writePosition - this.readPosition
                                        : this.memoryOwner.Memory.Length - this.readPosition;

                var toCopy = Math.Min(canRead, buffer.Length);

                var data = this.memoryOwner.Memory.Slice(this.readPosition, toCopy);

                data.CopyTo(buffer);

                read += toCopy;
                this.readPosition += toCopy;
                this.readPosition %= this.memoryOwner.Memory.Length;

                if(this.readPosition == this.writePosition)
                {
                    this.isEmpty = true;
                }

                this.waitForRead = false;

                if(this.waitForWrite && this.Length - this.Count >= this.WriteGate)
                {
                    lock(locker)
                    {
                        Monitor.Pulse(locker);
                    }
                }
                buffer = buffer.Slice(toCopy);
            }
            return new ValueTask<int>(read);
        }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        lock(locker)
        {
            this.isFlushed = true;
            return new ValueTask();
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var registration = cancellationToken.Register(() =>
        {
            lock(locker)
            {
                Monitor.PulseAll(locker);
            }
        });


            while(buffer.Length > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if(this.Count >= this.memoryOwner.Memory.Length)
                {

                    lock(locker)
                    {
                        this.waitForWrite = true;

                        if(this.waitForRead)
                        {
                            Monitor.Pulse(locker);
                        }

                        Monitor.Wait(locker);
                    }
   
                    continue;
                }

                var canWrite = this.isEmpty ? this.memoryOwner.Memory.Length - this.writePosition
                                       : this.writePosition > readPosition
                                            ? this.memoryOwner.Memory.Length - writePosition
                                            : this.readPosition - writePosition;

                var toCopy = Math.Min(canWrite, buffer.Length);

                var data = this.memoryOwner.Memory.Slice(this.writePosition, toCopy);

                buffer.Slice(0, toCopy).CopyTo(data);

                this.writePosition += toCopy;
                this.writePosition %= this.memoryOwner.Memory.Length;

                if(toCopy > 0)
                {
                    this.isEmpty = false;
                }

                this.waitForWrite = false;

                if(waitForRead)
                {
                    lock(locker)
                    {
                        Monitor.Pulse(locker);
                    }
                }
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
            this.waitForWrite = false;
            this.waitForWrite = false;
            Monitor.PulseAll(locker);
        }
    }
}
}
