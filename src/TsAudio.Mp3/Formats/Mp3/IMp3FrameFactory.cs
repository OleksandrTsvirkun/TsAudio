using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Formats.Mp3
{
    public interface IMp3FrameFactory
    {
        IAsyncEnumerable<(Mp3Index Index, Mp3Frame Frame)> LoadFrameHeadersAsync(Stream stream, CancellationToken cancellationToken = default);

        IAsyncEnumerable<Mp3Frame> LoadFramesAsync(Stream stream, CancellationToken cancellationToken = default);

        ValueTask<Mp3Frame?> LoadFrameAsync(Stream stream, Mp3Index index, CancellationToken cancellationToken = default);
    }
}
