using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Streams;

namespace TsAudio.Wave.WaveStreams;

public interface IWaveStreamFactory : IAsyncDisposable
{
    long SampleCount { get; }

    long? TotalSamples { get;  }

    Task<IWaveStream> GetWaveStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default);

    Task InitAsync(CancellationToken cancellationToken = default);
}
