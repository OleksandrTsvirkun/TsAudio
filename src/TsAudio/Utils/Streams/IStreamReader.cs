using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public interface IStreamReader : IAsyncDisposable
{
    long Position { get; set; }

    long Length { get; }

    StreamReadMode Mode { get; }

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}


