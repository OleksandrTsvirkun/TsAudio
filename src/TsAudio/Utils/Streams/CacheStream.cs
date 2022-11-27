using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class CacheStream : Stream
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly MemoryMappedViewStream writer;
    private long advance;
    private bool disposed;
    private readonly ManualResetEventSlim writeAwaiter = new(true);
    private readonly ManualResetEventSlim readAwaiter = new(false);
    private readonly IList<WeakReference<Reader>> readers = new List<WeakReference<Reader>>();

    public long Advance => this.advance;

    public bool WritingIsDone => this.writer.Position >= this.writer.Length;

    public class Reader : Stream
    {
        private readonly CacheStream parent;
        private readonly MemoryMappedViewStream reader;
        private bool disposed;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                this.ThrowIdDisposed();
                return this.reader.Length;
            }
        }

        public override long Position
        {
            get
            {
                this.ThrowIdDisposed();
                return this.reader.Position;
            }

            set
            {
                this.ThrowIdDisposed();
                this.reader.Position = Math.Min(Math.Max(value, 0), this.Length);
            }
        }

        public ReaderMode Mode { get; set; }

        internal Reader(MemoryMappedViewStream reader, CacheStream parent, ReaderMode mode = ReaderMode.Wait)
        {
            this.parent = parent;
            this.reader = reader;
            this.Mode = mode;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellatinoToken = default)
        {
            try
            {
                this.ThrowIdDisposed();


                return this.Mode switch
                {
                    ReaderMode.Kick => KickReadAsync(buffer, cancellatinoToken),
                    ReaderMode.Wait => WaitReadAsync(buffer, cancellatinoToken),
                    _ => throw new NotImplementedException()
                };
            }
            catch(Exception)
            {
                return new ValueTask<int>(0);
            }

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            this.ThrowIdDisposed();

            return this.reader.Seek(offset, origin);
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
            base.Dispose(disposing);
            if(disposing)
            {
                this.reader.Dispose();
                this.disposed = true;
            }
        }

        private async ValueTask<int> KickReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var canRead = (int)Math.Min(buffer.Length, this.parent.Length - this.Position);

            if(canRead == 0 && this.parent.WritingIsDone)
            {
                return 0;
            }

            var read = await this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);

            var advance = this.parent.advance;
            if(advance < this.reader.Position)
            {
                this.parent.advance = this.reader.Position;
            }

            if(this.parent.Position - this.parent.advance <= this.parent.ResumeWriterThreshold)
            {
                this.parent.writeAwaiter.Set();
            }

            return read;
        }

        private async ValueTask<int> WaitReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = 0;
            do
            {
                var readBlock = await this.OneReadAsync(buffer, cancellationToken);

                if (readBlock == 0 && this.parent.WritingIsDone)
                {
                    return 0;
                }

                buffer = buffer.Slice(readBlock);
                read += readBlock;

                if(buffer.Length > 0 && this.parent.Position < this.parent.Length)
                {
                    this.parent.readAwaiter.Reset();
                    this.parent.readAwaiter.Wait(cancellationToken);
                }
                else if(buffer.Length > 0)
                {
                    return read;
                }

            } while(buffer.Length > 0 && !cancellationToken.IsCancellationRequested);
            return read;
        }

        private ValueTask<int> OneReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var canRead = (int)Math.Min(buffer.Length, this.parent.Position - this.Position);

            if (canRead == 0 && this.parent.WritingIsDone)
            {
                return new ValueTask<int>(0);
            }

            return this.reader.ReadAsync(buffer.Slice(0, canRead), cancellationToken);
        }

        private void ThrowIdDisposed()
        {
            if(this.disposed)
            {
                throw new ObjectDisposedException("reader");
            }
        }
    }

    public CacheStream(long length)
    {
        this.memoryMapped = MemoryMappedFile.CreateNew(null, length);
        this.writer = this.memoryMapped.CreateViewStream(0, length, MemoryMappedFileAccess.Write);
        this.PauseWriterThreshold = 4096 * 4 * 16 * 4*4;
        this.ResumeWriterThreshold = 4096 * 4 * 4;
    }

    public async Task LoadAsync(Stream stream, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[bufferSize];
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

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length
    {
        get
        {
            this.ThrowIdDisposed();
            return this.writer.Length;
        }
    }

    public override long Position
    {
        get
        {
            this.ThrowIdDisposed();
            return this.writer.Position;
        }

        set => throw new NotImplementedException();
    }

    public int PauseWriterThreshold { get; set; }

    public int ResumeWriterThreshold { get; set; }

    public override void Flush()
    {
        this.ThrowIdDisposed();
        this.writer.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        this.ThrowIdDisposed();
        return this.writer.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.ThrowIdDisposed();

        this.writeAwaiter.Wait(cancellationToken);

        var toCopy = (int)Math.Min(buffer.Length, this.Length - this.Position);

        if(toCopy == 0)
        {
            return;
        }

        await this.writer.WriteAsync(buffer, cancellationToken);

        this.readAwaiter.Set();

        if(this.Position - this.advance > this.PauseWriterThreshold)
        {
            this.writeAwaiter.Reset();
            this.writeAwaiter.Wait(cancellationToken);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public Reader GetReader(ReaderMode mode = ReaderMode.Wait)
    {
        this.ThrowIdDisposed();

        var stream = this.memoryMapped.CreateViewStream(0, writer.Length, MemoryMappedFileAccess.Read);
        var reader = new Reader(stream, this, mode);
        return reader;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if(disposing)
        {
            this.writer.Dispose();
            this.memoryMapped.Dispose();

            foreach(var @ref in this.readers)
            {
                if(@ref.TryGetTarget(out var reader))
                {
                    reader.Dispose();
                }
            }

            this.disposed = true;
        }
    }

    private void ThrowIdDisposed()
    {
        if(this.disposed)
        {
            throw new ObjectDisposedException("reader");
        }
    }
}
