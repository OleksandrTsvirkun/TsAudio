using System;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class MemoryMappedStreamManagerReader : Stream
{
    private readonly MemoryMappedViewStream reader;
    private readonly ManualResetEventSlim readAwaiter;
    private readonly Func<long> getBuffered;
    private readonly Func<bool> isWritingDone;
    private readonly Action<long> setAdvance;

    private bool disposed;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => this.reader.Length;

    public override long Position
    {
        get => this.reader.Position;
        set => this.reader.Position = Math.Min(Math.Max(value, 0), this.Length);
    }

    public ReaderMode Mode { get; set; }

    internal MemoryMappedStreamManagerReader(MemoryMappedStreamManagerReaderArgs args, ReaderMode mode = ReaderMode.Wait)
    {
        this.getBuffered = args.GetBuffered;
        this.readAwaiter = args.ReadAwaiter;
        this.isWritingDone = args.GetWritingIsDone;
        this.setAdvance = args.SetAdvance;
        this.reader = args.Reader;
        this.Mode = mode;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellatinoToken = default)
    {
        return this.Mode switch
        {
            ReaderMode.Kick => this.KickReadAsync(buffer, cancellatinoToken),
            ReaderMode.Wait => this.WaitReadAsync(buffer, cancellatinoToken),
            _ => throw new NotImplementedException()
        };
    }
    public override long Seek(long offset, SeekOrigin origin)
    {
        return this.reader.Seek(offset, origin);
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        if(this.disposed)
        {
            return;
        }

        base.Dispose(disposing);
        if(disposing)
        {
            this.reader.Dispose();
            this.disposed = true;
        }
    }

    private async ValueTask<int> KickReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var canRead = (int)Math.Min(buffer.Length, this.Length - this.Position);

        if(canRead == 0 && this.isWritingDone())
        {
            return 0;
        }

        var read = await this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);

        this.setAdvance(this.reader.Position);

        return read;
    }

    private async ValueTask<int> WaitReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = 0;
        do
        {
            var readBlock = await this.OneReadAsync(buffer, cancellationToken);

            if(readBlock == 0 && this.isWritingDone())
            {
                return 0;
            }

            buffer = buffer.Slice(readBlock);
            read += readBlock;

            if(buffer.Length > 0 && this.getBuffered() < this.Length)
            {
                this.readAwaiter.Reset();
                this.readAwaiter.Wait(cancellationToken);
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
        var canRead = (int)Math.Min(buffer.Length, this.getBuffered() - this.Position);

        if(canRead == 0 && this.isWritingDone())
        {
            return new ValueTask<int>(0);
        }

        return this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);
    }
}

