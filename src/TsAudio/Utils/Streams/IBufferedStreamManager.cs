using System;
using System.Threading.Tasks;
using System.Threading;

namespace TsAudio.Utils.Streams;

public interface IBufferedStreamManager : IStreamManager, IAsyncDisposable
{
    long Buffered { get; }

    long Advanced { get; }

    long Capacity { get; }

    BufferingOptions BufferingOptions { get; }

    ValueTask WriteAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
