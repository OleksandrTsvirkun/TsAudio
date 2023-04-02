using System;

namespace TsAudio.Wave.WaveInputs;

public class CaptureStateEventArgs : EventArgs
{
    public CaptureState CaptureState { get; }

    public CaptureStateEventArgs(CaptureState captureState)
    {
        this.CaptureState = captureState;
    }
}
