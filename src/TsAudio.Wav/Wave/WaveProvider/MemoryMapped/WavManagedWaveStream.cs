namespace TsAudio.Wave.WaveProvider.MemoryMapped;

public class WavManagedWaveStream : WavWaveStream
{
    internal WavManagedWaveStream(WavManagedWaveStreamArgs args) : base(args.Reader)
    {
        this.metadata = args.Metadata;
    }

    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        if(this.metadata is null)
        {
            throw new ArgumentNullException(nameof(this.metadata));
        }

        this.stream.Seek(this.metadata.DataChunkPosition, SeekOrigin.Begin);
        return Task.CompletedTask;
    }
}
