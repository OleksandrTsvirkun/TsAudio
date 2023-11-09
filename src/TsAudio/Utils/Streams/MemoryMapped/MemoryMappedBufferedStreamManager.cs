using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Threading;

namespace TsAudio.Utils.Streams.MemoryMapped;

public class MemoryMappedBufferedStreamManager : IBufferedStreamManager
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly ManualResetEventSlim writeAwaiter = new(true);
    private readonly ManualResetEventSlim readAwaiter = new(false);
    private readonly ConcurrentBag<WeakReference<MemoryMappedBufferedStreamReader>> readers = new();
    private readonly MemoryMappedViewStream writer;

    private long advanced;
    public long Advanced => this.advanced;

    public bool WritingIsDone => this.writer is null ? true : this.writer.Position >= this.writer.Length;

    private long capacity;
    public long Capacity => this.capacity;

    public long Buffered => this.writer is null ? this.capacity : this.writer.Position;

    public BufferingOptions BufferingOptions { get; }

    public MemoryMappedBufferedStreamManager(FileStream fileStream, BufferingOptions bufferingOptions = null)
    {
        this.capacity = fileStream.Length;
        this.advanced = fileStream.Length;
        this.memoryMapped = MemoryMappedFile.CreateFromFile(fileStream, null, this.capacity, MemoryMappedFileAccess.Read, HandleInheritability.Inheritable, false);

        this.BufferingOptions = bufferingOptions ?? new BufferingOptions()
        {
            PauseWriterThreshold = 4096 * 4 * 16 * 4,
            ResumeWriterThreshold = 4096 * 4 * 4
        };
    }

    public MemoryMappedBufferedStreamManager(long capacity, BufferingOptions bufferingOptions = null)
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
        if(this.writer is null)
        {
            throw new InvalidOperationException("Writing is not allowed.");
        }

        await this.writeAwaiter.GetAwaiterWithSoftCancellation(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

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
        var reader = new MemoryMappedBufferedStreamReader(this, mode);
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

        if (this.writer is not null)
        {
            await this.writer.DisposeAsync();
        }

        this.memoryMapped.Dispose();
    }

    internal MemoryMappedViewStream CreateReader()
    {
        return this.memoryMapped.CreateViewStream(0, this.Capacity, MemoryMappedFileAccess.Read);
    }

    internal ManualResetEventSlimAwaiterWithCancellation WaitForReadAsync(CancellationToken cancellationToken = default)
    {
        return this.readAwaiter.ResetAndGetAwaiterWithSoftCancellation(cancellationToken);
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
