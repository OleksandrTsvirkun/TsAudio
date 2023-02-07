using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class BufferedStreamManager
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly MemoryMappedViewStream writer;
    private readonly ManualResetEventSlim writeAwaiter = new(true);
    private readonly ManualResetEventSlim readAwaiter = new(false);
    private readonly ConcurrentBag<WeakReference<BufferedStreamManagerReader>> readers = new ConcurrentBag<WeakReference<BufferedStreamManagerReader>>();

    private long advance;
    public long Advance
    {
        get => this.advance;
        set
        {
            if(value > this.advance)
            {
                this.advance = value;

                if(this.Buffered - this.advance <= this.BufferingOptions.ResumeWriterThreshold)
                {
                    this.writeAwaiter.Set();
                }
            }
        }
    }

    public bool WritingIsDone => this.buffered >= this.capacity;

    private long capacity;
    public long Capacity => this.capacity;

    private long buffered;
    public long Buffered => this.buffered;

    public bool CanWrite { get; }

    public BufferingOptions BufferingOptions { get; }

    public BufferedStreamManager(FileStream fileStream, string name = null)
    {
        this.CanWrite = false;

        this.capacity = fileStream.Length;
        this.advance = fileStream.Length;
        this.buffered = fileStream.Length;

        this.memoryMapped = MemoryMappedFile.CreateFromFile(fileStream, name, this.capacity, MemoryMappedFileAccess.Read, HandleInheritability.None, true);

        this.BufferingOptions = new BufferingOptions()
        {
            PauseWriterThreshold = int.MaxValue,
            ResumeWriterThreshold = 0
        };
    }

    public BufferedStreamManager(long capcity, BufferingOptions bufferingOptions = null) : this(null, capcity, bufferingOptions)
    {

    }

    public BufferedStreamManager(string name, long capacity, BufferingOptions bufferingOptions = null)
    {
        this.CanWrite = true;
        this.capacity = capacity;
        this.memoryMapped = MemoryMappedFile.CreateNew(name, this.capacity);
        this.writer = this.memoryMapped.CreateViewStream(0, this.capacity, MemoryMappedFileAccess.Write);
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
                await this.FlushAsync(cancellationToken);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }, cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        return this.writer.FlushAsync(cancellationToken);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if(!this.CanWrite)
        {
            throw new NotSupportedException();
        }

        this.writeAwaiter.Wait(cancellationToken);

        var toCopy = (int)Math.Min(buffer.Length, this.capacity - this.buffered);

        if(toCopy == 0)
        {
            return;
        }

        await this.writer.WriteAsync(buffer, cancellationToken);
        this.buffered = this.writer.Position;

        this.readAwaiter.Set();

        if(this.buffered - this.advance > this.BufferingOptions.PauseWriterThreshold)
        {
            this.writeAwaiter.Reset();
            this.writeAwaiter.Wait(cancellationToken);
        }
    }

    public ValueTask<Stream> GetBufferedStreamManagerReaderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var args = new BufferedStreamManagerReaderArgs()
        {
            ReadAwaiter = this.readAwaiter,
            SetAdvance = (value) => this.Advance = value,
            GetWritingIsDone = () => this.WritingIsDone,
            GetBuffered = () => this.Buffered,
            Reader = this.memoryMapped.CreateViewStream(0, this.capacity, MemoryMappedFileAccess.Read)
        };
        var reader = new BufferedStreamManagerReader(args, mode);
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
