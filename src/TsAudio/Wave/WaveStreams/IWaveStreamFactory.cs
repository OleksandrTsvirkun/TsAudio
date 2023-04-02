using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveStreams;

public interface IWaveStreamFactory : IDisposable, IAsyncDisposable
{
    long SampleCount { get; }

    long? TotalSamples { get;  }

    ValueTask<IWaveStream> GetWaveStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default);

    ValueTask InitAsync(CancellationToken cancellationToken = default);
}
