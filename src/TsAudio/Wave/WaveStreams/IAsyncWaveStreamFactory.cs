using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveStreams
{
    public interface IAsyncWaveStreamFactory : IDisposable
    {
        Task Loading { get; }

        long SampleCount { get; }

        WaveStreamMetadata Metadata { get; }

        ValueTask<IWaveStream> GetWaveProviderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default);

        ValueTask InitAsync(CancellationToken cancellationToken = default);
    }
}
