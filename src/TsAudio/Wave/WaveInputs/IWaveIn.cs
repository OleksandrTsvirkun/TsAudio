using System;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveInputs;

/// <summary>
/// Generic interface for wave recording
/// </summary>
public interface IWaveIn : IDisposable
{
    WaveFormat WaveFormat { get; }

    CaptureState CaptureState { get; }

    event EventHandler<WaveInEventArgs> DataAvailable;

    event EventHandler<CaptureStateEventArgs> CaptureStateChanged;

    void StartRecording();

    void StopRecording();
}
