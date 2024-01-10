using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStreamKickReaderArgs
{
    public ReaderWriterLockSlim Locker { get; init; }
    public Func<ReadOnlyMemory<byte>> GetBuffer { get; init; }
    public Func<long, CancellationToken, ValueTask> ConsumeAsync { get; init; }
    public Func<long, CancellationToken, ValueTask> SeekAsync { get; init; }
    public long Length { get; init; }   
}
