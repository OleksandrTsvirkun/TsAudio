using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public interface IStreamManager : IAsyncDisposable 
{
    ValueTask<Stream> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default);
}
