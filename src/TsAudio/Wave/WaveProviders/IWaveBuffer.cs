using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Wave.WaveProviders;

public interface IWaveBuffer : IWaveProvider
{
    /// <summary>
    /// Fill the buffer with wave data.
    /// </summary>
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark that filling of the buffer is completed (audio stream ended).
    /// </summary>
    ValueTask FlushAsync(Exception exception = null);

    /// <summary>
    /// Clean the buffer (audio stream position chnaged).
    /// </summary>
    ValueTask ResetAsync(Exception exception = null);
}
