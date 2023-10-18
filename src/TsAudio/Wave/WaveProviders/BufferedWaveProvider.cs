using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

using TsAudio.Utils.Threading;

namespace TsAudio.Wave.WaveProviders;

/// <summary>
/// Provides a buffered store of samples
/// Read method will return queued samples or fill buffer with zeroes
/// Now backed by a circular buffer
/// </summary>
public class BufferedWaveProvider : IWaveBuffer
{
    protected readonly SemaphoreSlim locker = new(1, 1);
    protected readonly ManualResetEventSlim readEvent = new(false);
    protected readonly ManualResetEventSlim writeEvent = new(true);

    protected IMemoryOwner<byte> memoryOwner;
    protected Memory<byte> memory;

    protected int writePosition;
    protected int readPosition;

    protected int writeGate;
    protected bool isEmpty;
    protected bool isFlushed;

    /// <summary>
    /// Gets the WaveFormat
    /// </summary>
    public WaveFormat WaveFormat { get; }

    public int Count => this.isEmpty ? 0 : this.writePosition > this.readPosition
                                    ? this.writePosition - this.readPosition
                                    : this.memory.Length - this.readPosition + this.writePosition;

    public bool AllowWait { get; set; } = true;

    public int WriteGate
    {
        get => this.writeGate;
        set => this.writeGate = Math.Max(0, Math.Min(this.memory.Length, value));
    }

    public long Length => this.memory.Length;

    public BufferedWaveProvider(WaveFormat waveFormat, int bufferSize = 4096, MemoryPool<byte> pool = null)
    {
        this.WaveFormat = waveFormat;
        this.memoryOwner = (pool ?? MemoryPool<byte>.Shared).Rent(bufferSize);
        this.memory = this.memoryOwner.Memory;
        this.WriteGate = (this.memoryOwner.Memory.Length / 4 * 3);
        this.isEmpty = true;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = 0;
        while(buffer.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            if(this.Count == 0 && this.isFlushed)
            {
                break;
            }
            else if(this.Count == 0 && this.AllowWait)
            {
                await this.readEvent.ResetAndGetAwaiterWithCancellation(cancellationToken);
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
                                : this.memory.Length - readPosition;

            var toCopy = Math.Min(canRead, buffer.Length);

            var data = this.memory.Slice(readPosition, toCopy);

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
                this.writeEvent.Set();
            }

            buffer = buffer.Slice(toCopy);
        }
        return read;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        this.isFlushed = true;
        this.readEvent.Set();
        return default;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var memory = this.memoryOwner.Memory;
        while(buffer.Length > 0 && !cancellationToken.IsCancellationRequested)
        {
            if(this.Count >= memory.Length)
            {
                this.writeEvent.Reset();
                await this.writeEvent.GetAwaiterWithCancellation(cancellationToken);
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

            this.readEvent.Set();

            buffer = buffer.Slice(toCopy);
        }
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        using var locker = await this.locker.LockAsync(cancellationToken);

        this.isFlushed = false;
        this.writePosition = 0;
        this.readPosition = 0;
        this.memoryOwner.Memory.Span.Clear();
        this.isEmpty = true;
        this.readEvent.Reset();
        this.writeEvent.Set();
    }
}
