using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Threading;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStreamWaitReader : IStreamReader
{
    private ReaderWriterLockSlim Locker { get; }
    private Func<IEnumerable<BufferMemorySegment<byte>>> GetBuffers { get; }
    private ManualResetEventSlim ReadAwaiter { get; }

    public long Length { get;  }

    public long Position { get; set; }

    public StreamReadMode Mode => StreamReadMode.Wait;

    public SeekableHttpContentStreamWaitReader(SeekableHttpContentStreamWaitReaderArgs args)
    {
        this.Locker = args.Locker;
        this.Length = args.Length;
        this.ReadAwaiter = args.ReadAwaiter;
        this.GetBuffers = args.GetBuffers;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        using var locker = this.Locker.AquireReadLock();

        var totalRead = 0;

        while(true)
        {
            var buffers = this.GetBuffers();
            var enumerator = buffers.GetEnumerator();

            bool canNext;
            while(canNext = enumerator.MoveNext())
            {
                var startPosition = enumerator.Current.Position;
                var endPosition = enumerator.Current.Position + enumerator.Current.Memory.Length;

                if (this.Position >= startPosition && this.Position < endPosition)
                {
                    continue;
                }
            }

            if(!canNext)
            {
                await this.ReadAwaiter.ResetAndGetAwaiterWithSoftCancellation(cancellationToken);
                continue;
            }

            do
            {
                var memory = enumerator.Current.Memory;
                var offset = (int)(this.Position - enumerator.Current.Position);
                memory = memory.Slice(offset);

                var toCopy = Math.Min(memory.Length, buffer.Length);
                var toCopyMemory = memory.Slice(0, toCopy);
                toCopyMemory.CopyTo(buffer);
                memory = memory.Slice(toCopy);
                buffer = buffer.Slice(toCopy);
                this.Position += toCopy;
                totalRead += toCopy;
            } while(buffer.Length > 0 && enumerator.MoveNext());

            if (buffer.Length == 0)
            {
                break;
            }
        }

        return totalRead;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}