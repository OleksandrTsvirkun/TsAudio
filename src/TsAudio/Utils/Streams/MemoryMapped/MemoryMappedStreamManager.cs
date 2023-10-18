using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Threading;

namespace TsAudio.Utils.Streams.MemoryMapped;

public class MemoryMappedStreamManager : IBufferedStreamManager
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly ManualResetEventSlim writeAwaiter = new(true);
    private readonly ManualResetEventSlim readAwaiter = new(false);
    private readonly ConcurrentBag<WeakReference<MemoryMappedStreamManagerReader>> readers = new();
    private readonly MemoryMappedViewStream writer;

    private long advanced;
    public long Advanced => this.advanced;

    public bool WritingIsDone => this.writer.Position >= this.writer.Length;

    private long capacity;
    public long Capacity => this.capacity;

    public long Buffered => this.writer.Position;

    public BufferingOptions BufferingOptions { get; }

    public MemoryMappedStreamManager(long capacity = long.MaxValue, BufferingOptions bufferingOptions = null)
    {
        this.memoryMapped = MemoryMappedFile.CreateNew(Path.GetRandomFileName(), capacity);
        this.writer = this.memoryMapped.CreateViewStream(0, capacity, MemoryMappedFileAccess.Write);
        this.capacity = this.writer.Length;

        this.BufferingOptions = bufferingOptions ?? new BufferingOptions()
        {
            PauseWriterThreshold = 4096 * 4 * 16 * 4,
            ResumeWriterThreshold = 4096 * 4 * 4
        };
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return this.writer.FlushAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await this.writeAwaiter.GetAwaiterWithCancellation(cancellationToken);

        var toCopy = (int)Math.Max(0, Math.Min(buffer.Length, this.Capacity - this.Buffered));

        if(toCopy == 0)
        {
            return;
        }

        await this.writer.WriteAsync(buffer, cancellationToken);

        this.readAwaiter.Set();

        if(this.Buffered - this.advanced > this.BufferingOptions.PauseWriterThreshold)
        {
            this.writeAwaiter.Reset();
        }
    }

    public ValueTask<Stream> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = new MemoryMappedStreamManagerReader(this, mode);
        return new ValueTask<Stream>(reader);
    }

    public async ValueTask DisposeAsync()
    {
        foreach(var @ref in this.readers)
        {
            if(@ref.TryGetTarget(out var reader))
            {
                await reader.DisposeAsync();
            }
        }

        await this.writer.DisposeAsync();
        this.memoryMapped.Dispose();
    }

    internal MemoryMappedViewStream CreateReader()
    {
        return this.memoryMapped.CreateViewStream(0, this.Capacity, MemoryMappedFileAccess.Read);
    }

    internal ManualResetEventSlimAwaiterWithCancellation WaitForReadAsync(CancellationToken cancellationToken = default)
    {
        return this.readAwaiter.ResetAndGetAwaiterWithCancellation(cancellationToken);
    }

    internal void WaitForRead()
    {
        this.readAwaiter.Reset();
        this.readAwaiter.Wait();
    }

    internal void Advance(long value)
    {
        ArithmeticalExtensions.SetGreater(ref this.advanced, value);

        if(this.Buffered - this.advanced <= this.BufferingOptions.ResumeWriterThreshold)
        {
            this.writeAwaiter.Set();
        }
    }
}
