using System;

namespace TsAudio.Wave.WaveInputs;

/// <summary>
/// Event Args for WaveInStream event
/// </summary>
public class WaveInEventArgs : EventArgs
{
    /// <summary>
    /// Buffer containing recorded data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Creates new WaveInEventArgs
    /// </summary>
    public WaveInEventArgs(ReadOnlyMemory<byte> data)
    {
        this.Data = data;
    }
}
