using System;
using System.Threading.Tasks;

using TsAudio.Sample.PeekProviders;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveForm;

public class WaveFormRendererData : IAsyncDisposable
{
    public IWaveStream WaveStream { get; init; }

    public long? TotalSamples { get; init; }

    public IPeakProvider PeakProvider { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (this.PeakProvider is not null)
        {
            await this.PeakProvider.DisposeAsync();
        }

        if(this.WaveStream is not null)
        {
            await this.WaveStream.DisposeAsync();
        }
    }
}
