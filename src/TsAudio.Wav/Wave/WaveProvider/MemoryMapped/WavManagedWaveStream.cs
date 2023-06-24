namespace TsAudio.Wav.Wave.WaveProvider.MemoryMapped;

public class WavManagedWaveStream : WavWaveStream
{
    internal WavManagedWaveStream(WavManagedWaveStreamArgs args) : base(args.Reader)
    {
        this.metadata = args.Metadata;
    }

    public override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        this.stream.Seek(this.metadata.DataChunkPosition, SeekOrigin.Begin);
        return ValueTask.CompletedTask;
    }
}
