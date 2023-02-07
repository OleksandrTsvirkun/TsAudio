using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveStreams
{
    public interface IAsyncWaveStreamFactory : IDisposable
    {
        Task Parsing { get; }

        long SampleCount { get; }

        long? TotalSamples { get; }

        ValueTask<IWaveStream> GetWaveProviderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default);

        Task InitAsync(CancellationToken cancellationToken = default);
    }
}
