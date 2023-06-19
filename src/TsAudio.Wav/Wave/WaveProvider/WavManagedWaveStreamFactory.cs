using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wav.Wave.WaveProvider;

public class WavWaveStreamFactory : IWaveStreamFactory
{
    private readonly IStreamManager streamManager;
    private readonly IWavFormatMetadataReader metadataReader;
    private WavMetadata metadata;
    
    public long SampleCount => this.metadata.DataChuckLength / ((this.metadata.WaveFormat.BitsPerSample / 8) * this.metadata.WaveFormat.Channels);

    public long? TotalSamples => this.SampleCount;

    public WaveFormat WaveFormat => this.metadata.WaveFormat ?? throw new InvalidOperationException("Must call init first.");

    public WavWaveStreamFactory(IStreamManager streamManager, IWavFormatMetadataReader metadataReader = null)
    {
        this.streamManager = streamManager;
        this.metadataReader = metadataReader ?? WavFormatMetadataReader.Instance;
    }

    public async ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        using var stream = await this.streamManager.GetStreamAsync(StreamReadMode.Kick, cancellationToken);

        this.metadata = await this.metadataReader.ReadWavFormatMetadataAsync(stream, cancellationToken);
    }

    public async  ValueTask<IWaveStream> GetWaveStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var args = new WavManagedWaveStreamArgs()
        {
            DataChuckLength = this.metadata.DataChuckLength,
            DataChuckPosition = this.metadata.DataChuckPosition,
            WaveFormat = this.metadata.WaveFormat,
            Reader = await this.streamManager.GetStreamAsync(mode, cancellationToken),
        };

        return new WavManagedWaveStream(args); 
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }



}
