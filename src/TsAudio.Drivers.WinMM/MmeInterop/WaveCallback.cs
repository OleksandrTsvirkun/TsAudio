using System;

namespace TsAudio.Drivers.WinMM.MmeInterop
{
    // use the userdata as a reference
    // WaveOutProc https://docs.microsoft.com/en-us/previous-versions//dd743869(v=vs.85)?redirectedfrom=MSDN
    // WaveInProc http://msdn.microsoft.com/en-us/library/dd743849%28VS.85%29.aspx
    public delegate void WaveCallback(IntPtr hWaveOut, WaveMessage message, IntPtr dwInstance, WaveHeader wavhdr, IntPtr dwReserved);
}
