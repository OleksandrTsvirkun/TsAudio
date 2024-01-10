using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

public class Mp3ManagedWaveStreamFactoryArgs
{
    public required IStreamManager StreamManager { get; init; }

    public required long? TotalSamples { get; init; }

    public required int BufferSize { get; init; }

    public required bool DisposeStreamManager { get; init; }
}

