using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public interface IStreamManager : IAsyncDisposable 
{
    ValueTask<IStreamReader> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default);
}
