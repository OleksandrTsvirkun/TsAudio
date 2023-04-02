using System;

namespace TsAudio.Wave.WaveOutputs;

public class PlaybackStateEventArgs : EventArgs
{
    public PlaybackState PlaybackState { get; }

    public PlaybackStateEventArgs(PlaybackState playbackState)
    {
        this.PlaybackState = playbackState;
    }
}
