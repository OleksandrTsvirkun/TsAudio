using System;
using System.Collections.Generic;
using System.Threading;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStreamWaitReaderArgs
{
    public ReaderWriterLockSlim Locker { get; init; }
    public Func<IEnumerable<BufferMemorySegment<byte>>> GetBuffers { get; init; }
    public long Length { get; init; }
    public ManualResetEventSlim ReadAwaiter { get; init; }
}
