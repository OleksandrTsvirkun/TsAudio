using System;

namespace TsAudio.Wave.WaveOutputs;
public class PlaybackStateArgs : EventArgs
{
    public PlaybackState PlaybackState { get; }

    public PlaybackStateArgs(PlaybackState playbackState)
    {
        this.PlaybackState = playbackState;
    }
}
