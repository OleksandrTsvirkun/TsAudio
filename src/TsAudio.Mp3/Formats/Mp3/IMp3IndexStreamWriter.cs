using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TsAudio.Formats.Mp3;

public interface IMp3IndexStreamWriter : IDisposable, IAsyncDisposable
{
    ValueTask WriteAsync(IReadOnlyList<Mp3Index> indices, CancellationToken cancellationToken);
}
