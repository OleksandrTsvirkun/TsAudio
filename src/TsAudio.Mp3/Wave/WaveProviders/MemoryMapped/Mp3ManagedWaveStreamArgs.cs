using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

internal class Mp3ManagedWaveStreamArgs
{
    public required IMp3FrameFactory FrameFactory { get; init; }

    public required IReadOnlyList<Mp3Index> Indices { get; init; }

    public long? TotalSamples { get; init; }

    public required Task Analyzing { get; init; }

    public required Mp3WaveFormat Mp3WaveFormat { get; init; }

    public required IStreamReader Reader { get; init; }

    public required ManualResetEventSlim ParseWait { get; init; }

    public required int BufferSize { get; init; }
}

