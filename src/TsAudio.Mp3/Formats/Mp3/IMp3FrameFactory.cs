using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Streams;

namespace TsAudio.Formats.Mp3;

public interface IMp3FrameFactory
{
    IAsyncEnumerable<Mp3FrameIndex> LoadFrameIndicesAsync(IStreamReader stream, int bufferSize = 4096, CancellationToken cancellationToken = default);

    ValueTask<Mp3Frame?> LoadFrameAsync(IStreamReader stream, Mp3Index index, CancellationToken cancellationToken = default);
}
