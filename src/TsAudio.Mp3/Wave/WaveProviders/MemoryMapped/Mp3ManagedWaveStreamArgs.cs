using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

internal class Mp3ManagedWaveStreamArgs
{
    public IMp3FrameFactory FrameFactory { get; init; }

    public IReadOnlyList<Mp3Index> Indices { get; init; }

    public long? TotalSamples { get; init; }

    public Task Analyzing { get; init; }

    public Mp3WaveFormat Mp3WaveFormat { get; init; }

    public Stream Reader { get; init; }

    public ManualResetEventSlim ParseWait { get; init; }

    public int BufferSize { get; init; }
}

