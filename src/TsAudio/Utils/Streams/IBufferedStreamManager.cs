using System;

namespace TsAudio.Utils.Streams;

public interface IBufferedStreamManager : IStreamManager, IAsyncDisposable, IDisposable
{
    long Buffered { get; }

    long Advanced { get; }

    long Capacity { get; }
}
