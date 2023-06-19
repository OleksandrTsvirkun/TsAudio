using System;
using System.Buffers;
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
    private MemoryMappedViewStream writer;

    private long advanced;
    public long Advanced => this.advanced;

    public bool WritingIsDone => this.writer is null ? true : this.writer.Position >= this.writer.Length;

    private long capacity;
    public long Capacity => this.capacity;

    public long Buffered => this.writer is null ? this.capacity : this.writer.Position;

    public BufferingOptions BufferingOptions { get; }

    public MemoryMappedStreamManager(long capacity, BufferingOptions bufferingOptions = null)
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

    public Task LoadAsync(Stream stream, MemoryPool<byte> memoryPool = null, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        return Task.Factory.StartNew(LoadAsyncImpl, cancellationToken, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent, TaskScheduler.Default).Unwrap();

        async Task LoadAsyncImpl()
        {
            memoryPool ??= MemoryPool<byte>.Shared;

            var memoryOwner = memoryPool.Rent(bufferSize);
            try
            {
                while(!cancellationToken.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(memoryOwner.Memory, cancellationToken);

                    if(read == 0)
                    {
                        break;
                    }

                    await this.WriteAsync(memoryOwner.Memory.Slice(0, read), cancellationToken);
                }
            }
            catch(OperationCanceledException ex)
            {

            }
            catch(Exception ex)
            {

            }
            finally
            {
                memoryOwner.Dispose();
                await this.FlushAsync(cancellationToken);
                await this.writer.DisposeAsync();
                this.writer = null;
            }
        }
    }

    private Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (this.writer is null)
        {
            throw new InvalidOperationException("You cannot flush after data is loaded.");
        }

        return this.writer.FlushAsync(cancellationToken);
    }

    private async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if(this.writer is null)
        {
            throw new InvalidOperationException("You cannot write after data is loaded.");
        }

        await this.writeAwaiter.WaitAsync(cancellationToken);

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
            await this.writeAwaiter.WaitAsync(cancellationToken);
        }
    }

    public ValueTask<Stream> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = new MemoryMappedStreamManagerReader(this, mode);
        return new ValueTask<Stream>(reader);
    }

    public void Dispose()
    {
        foreach(var @ref in this.readers)
        {
            if(@ref.TryGetTarget(out var reader))
            {
                reader.Dispose();
            }
        }

        this.writer.Dispose();
        this.memoryMapped.Dispose();
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

    internal ValueTask WaitForReadAsync(CancellationToken cancellationToken = default)
    {
        this.readAwaiter.Reset();
        return this.readAwaiter.WaitAsync(cancellationToken);
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
