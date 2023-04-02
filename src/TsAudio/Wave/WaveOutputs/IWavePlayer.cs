using System;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Wave.WaveOutputs;

/// <summary>
/// Represents the interface to a device that can play a WaveFile
/// </summary>
public interface IWavePlayer : IDisposable
{
    /// <summary>
    /// The volume 
    /// 1.0f is full scale
    /// Note that not all implementations necessarily support volume changes
    /// </summary>
    float Volume { get; set; }

    /// <summary>
    /// Current playback state
    /// </summary>
    PlaybackState PlaybackState { get; }

    /// <summary>
    /// The WaveFormat this device is using for playback
    /// </summary>
    WaveFormat WaveFormat { get; }


    event EventHandler<PlaybackStateEventArgs> PlaybackStateChanged;

    /// <summary>
    /// Begin playback
    /// </summary>
    void Play();

    /// <summary>
    /// Stop playback
    /// </summary>
    void Stop();

    /// <summary>
    /// Pause Playback
    /// </summary>
    void Pause();

    /// <summary>
    /// Initialise playback
    /// </summary>
    /// <param name="waveProvider">The waveprovider to be played</param>
    void Init(IWaveProvider waveProvider);
}
