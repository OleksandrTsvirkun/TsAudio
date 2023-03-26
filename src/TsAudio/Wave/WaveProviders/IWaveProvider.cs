using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders;

/// <summary>
/// Generic interface for all WaveProviders.
/// </summary>
public interface IWaveProvider
{
    /// <summary>
    /// Gets the WaveFormat of this WaveProvider.
    /// </summary>
    /// <value>The wave format.</value>
    WaveFormat WaveFormat { get; }

    /// <summary>
    /// Fill the specified buffer with wave data.
    /// </summary>
    /// <param name="buffer">The buffer to fill of wave data.</param>
    /// <returns>the number of bytes written to the buffer.</returns>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
