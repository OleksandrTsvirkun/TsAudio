using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Formats.Mp3;

public interface IMp3FrameFactory
{
    IAsyncEnumerable<(Mp3FrameHeader Frame, Mp3Index Index)> LoadFrameIndicesAsync(Stream stream, int bufferSize = 4096, CancellationToken cancellationToken = default);

    ValueTask<Mp3Frame?> LoadFrameAsync(Stream stream, Mp3Index index, CancellationToken cancellationToken = default);
}
