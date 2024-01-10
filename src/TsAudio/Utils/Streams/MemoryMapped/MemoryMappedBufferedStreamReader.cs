using System;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.MemoryMapped;

public class MemoryMappedBufferedStreamReader : Stream, IStreamReader
{
    private readonly MemoryMappedBufferedStreamManager streamManager;
    private readonly MemoryMappedViewStream reader;

    private bool disposed;

    public sealed override bool CanRead => true;

    public override bool CanSeek => true;

    public sealed override bool CanWrite => false;

    public override long Length => this.reader.Length;

    public override long Position
    {
        get => this.reader.Position;
        set => this.reader.Position = Math.Clamp(value, 0, this.Length);
    }

    public StreamReadMode Mode { get; }

    internal MemoryMappedBufferedStreamReader(MemoryMappedBufferedStreamManager streamManager, StreamReadMode mode = StreamReadMode.Wait)
    {
        this.streamManager = streamManager;
        this.Mode = mode;
        this.reader = this.streamManager.CreateReader();
    }

    #region Async Read

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
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
        }
        while(!cancellationToken.IsCancellationRequested && buffer.Length > 0);

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
    #endregion


    #region Sync Read

    public override int Read(byte[] buffer, int offset, int count)
    {
        return this.Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        return this.Mode switch
        {
            StreamReadMode.Kick => this.KickRead(buffer),
            StreamReadMode.Wait => this.WaitRead(buffer),
            _ => throw new NotImplementedException()
        };
    }

    private int KickRead(Span<byte> buffer)
    {
        var canRead = (int)Math.Max(0, Math.Min(buffer.Length, this.Length - this.Position));

        if(canRead == 0 && this.streamManager.WritingIsDone)
        {
            return 0;
        }

        var read = this.reader.Read(buffer.Slice(0, canRead));

        this.streamManager.Advance(this.reader.Position);

        return read;
    }

    private int WaitRead(Span<byte> buffer)
    {
        var read = 0;

        do
        {
            var readBlock = this.OneRead(buffer);

            if(readBlock == 0 && this.streamManager.WritingIsDone)
            {
                return 0;
            }

            buffer = buffer.Slice(readBlock);
            read += readBlock;

            if(buffer.Length > 0 && this.streamManager.Buffered < this.Length)
            {
                this.streamManager.WaitForRead();
            }
            else if(buffer.Length > 0)
            {
                return read;
            }
        }
        while(buffer.Length > 0);

        return read;
    }

    private int OneRead(Span<byte> buffer)
    {
        var canRead = (int)Math.Max(0, Math.Min(buffer.Length, this.streamManager.Buffered - this.Position));

        if(canRead == 0 && this.streamManager.WritingIsDone)
        {
            return 0;
        }

        return this.reader.Read(buffer.Slice(0, canRead));
    }
    #endregion


    public override long Seek(long offset, SeekOrigin origin)
    {
        switch(origin)
        {
            case SeekOrigin.Begin:
                return this.reader.Position = Math.Clamp(offset, 0, this.Length);
            case SeekOrigin.Current:
                return Math.Clamp(this.reader.Position + offset, 0, this.Length);  
            case SeekOrigin.End:
                return Math.Clamp(this.reader.Position - offset, 0, this.Length);
            default:
                throw new ArgumentException("The provided origin enum has wrong value.", nameof(origin));
        }
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

    public sealed override void Flush()
    {
        throw new NotImplementedException();
    }

    public sealed override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public sealed override Task FlushAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

}

