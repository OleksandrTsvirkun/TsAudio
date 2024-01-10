using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Threading;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStreamKickReader : IStreamReader
{
    private ReaderWriterLockSlim Locker { get; }
    private Func<ReadOnlyMemory<byte>> GetBuffer { get; }
    private Func<long, CancellationToken, ValueTask> ConsumeAsync { get; }
    private Func<long, CancellationToken, ValueTask> SeekAsync { get; }

    public long Length { get;  }

    public long Position { get; set; }

    public StreamReadMode Mode => StreamReadMode.Kick;

    public SeekableHttpContentStreamKickReader(SeekableHttpContentStreamKickReaderArgs args)
    {
        this.Locker = args.Locker;
        this.GetBuffer = args.GetBuffer;
        this.ConsumeAsync = args.ConsumeAsync;
        this.SeekAsync = args.SeekAsync;
        this.Length = args.Length;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var locker = this.Locker.AquireWriteLock();

        await this.SeekAsync(this.Position, cancellationToken).ConfigureAwait(false);

        var totalRead = 0;
        while(buffer.Length > 0)
        {
            var memory = this.GetBuffer();

            var toCopy = Math.Min(memory.Length, buffer.Length);

            var toCopyMemory = memory.Slice(0, toCopy);
            toCopyMemory.CopyTo(buffer);
            await this.ConsumeAsync(this.Position + toCopy, cancellationToken).ConfigureAwait(false);
            memory = memory.Slice(toCopy);
            buffer = buffer.Slice(toCopy);
            totalRead += toCopy;
            this.Position += toCopy;
        }
        return totalRead;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}