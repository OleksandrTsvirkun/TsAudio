﻿using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Wave.WaveStreams;

public interface IWaveStream : IWaveProvider, IAsyncDisposable
{
    /// <summary>
    /// Returns SampleCount
    /// </summary>
    long? TotalSamples { get; }

    long Position { get; }

    ValueTask InitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="position">Position in samples</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default);
}
