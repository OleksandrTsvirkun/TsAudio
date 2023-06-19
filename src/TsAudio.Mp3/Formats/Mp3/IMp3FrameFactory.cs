using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Formats.Mp3;

public struct Mp3FrameIndex
{
    public Mp3Index Index { get; set; }

    public Mp3FrameHeader Frame { get; set; }
}

public interface IMp3FrameFactory
{
    IAsyncEnumerable<Mp3FrameIndex> LoadFrameIndicesAsync(Stream stream, int bufferSize = 4096, CancellationToken cancellationToken = default);

    ValueTask<Mp3Frame?> LoadFrameAsync(Stream stream, Mp3Index index, CancellationToken cancellationToken = default);
}
