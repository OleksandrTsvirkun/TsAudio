using TsAudio.Utils.Streams;
using TsAudio.Formats.Wav;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveProvider.MemoryMapped;

public class WavWaveStreamFactory : IWaveStreamFactory
{
    private readonly IStreamManager streamManager;
    private readonly IWavFormatMetadataReader metadataReader;
    private WavMetadata? metadata;

    public long SampleCount 
    { 
        get
        {
            if(this.metadata is null)
            {
                throw new InvalidOperationException("Must call init first.");
            }

            return this.metadata.DataChunkLength / (this.metadata.WaveFormat.BitsPerSample / 8 * this.metadata.WaveFormat.Channels); 
        }
    } 

    public long? TotalSamples => this.SampleCount;

    public WaveFormat WaveFormat => this.metadata?.WaveFormat ?? throw new InvalidOperationException("Must call init first.");

    public WavWaveStreamFactory(IStreamManager streamManager, IWavFormatMetadataReader? metadataReader = null)
    {
        this.streamManager = streamManager;
        this.metadataReader = metadataReader ?? WavFormatMetadataReader.Instance;
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        using var stream = await this.streamManager.GetStreamAsync(StreamReadMode.Wait, cancellationToken);

        this.metadata = await this.metadataReader.ReadWavFormatMetadataAsync(stream, cancellationToken);
    }

    public async ValueTask<IWaveStream> GetWaveStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        if(this.metadata is null)
        {
            throw new ArgumentNullException(nameof(this.metadata));
        }

        var reader = await this.streamManager.GetStreamAsync(mode, cancellationToken);
        var args = new WavManagedWaveStreamArgs()
        {
            Metadata = this.metadata ,
            Reader = reader,
        };

        return new WavManagedWaveStream(args);
    }

    public async ValueTask DisposeAsync()
    {
        await this.streamManager.DisposeAsync().ConfigureAwait(false);
    }

}
