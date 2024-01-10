using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Wave.WaveStreams;

public interface IWaveStream : IWaveProvider, IAsyncDisposable
{
    /// <summary>
    /// Returns SampleCount
    /// </summary>
    long? Length { get; }

    long Position { get; set; }

    Task InitAsync(CancellationToken cancellationToken = default);
}
