using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class MemoryMappedStreamManager : IStreamManager, IDisposable
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly MemoryMappedViewStream writer;
    private readonly ManualResetEventSlim writeAwaiter = new(true);
    private readonly ManualResetEventSlim readAwaiter = new(false);
    private readonly ConcurrentBag<WeakReference<MemoryMappedStreamManagerReader>> readers = new ConcurrentBag<WeakReference<MemoryMappedStreamManagerReader>>();

    private long advance;
    public long Advance 
    {
        get => this.advance;
        set
        {
            if (value > this.advance)
            {
                this.advance = value;

                if(this.Buffered - this.advance <= this.BufferingOptions.ResumeWriterThreshold)
                {
                    this.writeAwaiter.Set();
                }
            }
        }
    }

    public bool WritingIsDone => this.writer.Position >= this.writer.Length;

    public long Capacity => this.writer.Length;

    public long Buffered => this.writer.Position;

    public BufferingOptions BufferingOptions { get; }

    public MemoryMappedStreamManager(long capcity, BufferingOptions bufferingOptions = null) : this(null, capcity, bufferingOptions)
    {

    }

    public MemoryMappedStreamManager(string name, long capacity, BufferingOptions bufferingOptions = null)
    {
        this.memoryMapped = MemoryMappedFile.CreateNew(name, capacity);
        this.writer = this.memoryMapped.CreateViewStream(0, capacity, MemoryMappedFileAccess.Write);
        this.BufferingOptions = bufferingOptions ?? new BufferingOptions() 
        {
            PauseWriterThreshold = 4096 * 4 * 16 * 4,
            ResumeWriterThreshold = 4096 * 4 * 4
        };
    }

    public Task LoadAsync(Stream stream, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                while(true)
                {

                    var read = await stream.ReadAsync(buffer, cancellationToken);

                    if(read == 0)
                    {
                        break;
                    }

                    await this.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            catch(Exception ex)
            {

            }
            finally
            {
                await this.FlushAsync();
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }, cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return this.writer.FlushAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.writeAwaiter.Wait(cancellationToken);

        var toCopy = (int)Math.Min(buffer.Length, this.Capacity - this.Buffered);

        if(toCopy == 0)
        {
            return;
        }

        await this.writer.WriteAsync(buffer, cancellationToken);

        this.readAwaiter.Set();

        if(this.Buffered - this.advance > this.BufferingOptions.PauseWriterThreshold)
        {
            this.writeAwaiter.Reset();
            this.writeAwaiter.Wait(cancellationToken);
        }
    }

    public ValueTask<Stream> GetStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var args = new MemoryMappedStreamManagerReaderArgs()
        {
            ReadAwaiter = this.readAwaiter,
            SetAdvance = (value) => this.Advance = value,
            GetWritingIsDone = () => this.WritingIsDone,
            GetBuffered = () => this.Buffered,
            Reader = this.memoryMapped.CreateViewStream(0, this.Capacity, MemoryMappedFileAccess.Read)
        };
        var reader = new MemoryMappedStreamManagerReader(args, mode);
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
}
