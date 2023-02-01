using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace TsAudio.Utils.Streams;

internal class BufferedStreamManagerReaderArgs
{
    public ManualResetEventSlim ReadAwaiter { get; init; }

    public MemoryMappedViewStream Reader { get; init; }

    public Func<long> GetBuffered { get; init; }

    public Func<bool> GetWritingIsDone { get; init; }

    public Action<long> SetAdvance { get; init; }
}
