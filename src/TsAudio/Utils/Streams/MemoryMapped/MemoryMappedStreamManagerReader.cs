using System;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.MemoryMapped;

public class MemoryMappedStreamManagerReader : AsyncStream
{
    private readonly MemoryMappedStreamManager streamManager;
    private readonly MemoryMappedViewStream reader;

    private bool disposed;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => this.reader.Length;

    public override long Position
    {
        get => this.reader.Position;
        set => this.reader.Position = Math.Clamp(value, 0, this.Length);
    }

    public StreamReadMode Mode { get; }

    internal MemoryMappedStreamManagerReader(MemoryMappedStreamManager streamManager, StreamReadMode mode = StreamReadMode.Wait)
    {
        this.streamManager = streamManager;
        this.Mode = mode;
        this.reader = this.streamManager.CreateReader();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellatinoToken = default)
    {
        return this.Mode switch
        {
            StreamReadMode.Kick => this.KickReadAsync(buffer, cancellatinoToken),
            StreamReadMode.Wait => this.WaitReadAsync(buffer, cancellatinoToken),
            _ => throw new NotImplementedException()
        };
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        return this.reader.Seek(offset, origin);
    }

    protected override void Dispose(bool disposing)
    {
        if(this.disposed)
        {
            return;
        }

        if(disposing)
        {
            this.reader.Dispose();
        }

        this.disposed = true;
    }

    public async override ValueTask DisposeAsync()
    {
        if(this.disposed)
        {
            return;
        }

        await this.reader.DisposeAsync();
        this.disposed = true;
    }

    private async ValueTask<int> KickReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var canRead = (int)Math.Max(0, Math.Min(buffer.Length, this.Length - this.Position));

        if(canRead == 0 && this.streamManager.WritingIsDone)
        {
            return 0;
        }

        var read = await this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);

        this.streamManager.Advance(this.reader.Position);

        return read;
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async ValueTask<int> WaitReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = 0;
        do
        {
            var readBlock = await this.OneReadAsync(buffer, cancellationToken);

            if(readBlock == 0 && this.streamManager.WritingIsDone)
            {
                return 0;
            }

            buffer = buffer.Slice(readBlock);
            read += readBlock;

            if(buffer.Length > 0 && this.streamManager.Buffered < this.Length)
            {
                await this.streamManager.WaitForReadAsync(cancellationToken);
            }
            else if(buffer.Length > 0)
            {
                return read;
            }

        } while(buffer.Length > 0);
        return read;
    }

    private ValueTask<int> OneReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var canRead = (int)Math.Max(0, Math.Min(buffer.Length, this.streamManager.Buffered - this.Position));

        if(canRead == 0 && this.streamManager.WritingIsDone)
        {
            return new ValueTask<int>(0);
        }

        return this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);
    }


}

