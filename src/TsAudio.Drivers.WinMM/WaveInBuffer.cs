using System;
using TsAudio.Drivers.WinMM.MmeInterop;

namespace TsAudio.Drivers.WinMM;

/// <summary>
/// A buffer of Wave samples
/// </summary>
public class WaveInBuffer : WaveBuffer, IDisposable
{
    /// <summary>
    /// Provides access to the actual record buffer (for reading only)
    /// </summary>
    public ReadOnlyMemory<byte> Data => this.buffer.AsMemory(..this.header.bytesRecorded);

    /// <summary>
    /// Indicates whether the Done flag is set on this buffer
    /// </summary>
    public bool Done => (this.header.flags & WaveHeaderFlags.Done) == WaveHeaderFlags.Done;

    /// <summary>
    /// Indicates whether the InQueue flag is set on this buffer
    /// </summary>
    public bool InQueue => (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;

    /// <summary>
    /// creates a new wavebuffer
    /// </summary>
    /// <param name="waveInHandle">WaveIn device to write to</param>
    /// <param name="bufferSize">Buffer size in bytes</param>
    public WaveInBuffer(IntPtr waveInHandle, int bufferSize) : base(waveInHandle, bufferSize, new object())
    {
        WaveInteropExtensions.WaveInPrepareHeader(this.hWave, this.header, this.waveLock);
        WaveInteropExtensions.WaveInAddBuffer(this.hWave, this.header, this.waveLock);
    }

    /// <summary>
    /// Place this buffer back to record more audio
    /// </summary>
    public void Reuse()
    {
        WaveInteropExtensions.WaveInUnprepareHeader(this.hWave, this.header, this.waveLock);
        WaveInteropExtensions.WaveInPrepareHeader(this.hWave, this.header, this.waveLock);
        WaveInteropExtensions.WaveInAddBuffer(this.hWave, this.header, this.waveLock);
    }

    /// <summary>
    /// Releases resources held by this WaveBuffer
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if(this.hWave != IntPtr.Zero)
        {
            WaveInteropExtensions.WaveInUnprepareHeader(this.hWave, this.header, this.waveLock);
            this.hWave = IntPtr.Zero;
        }
        base.Dispose(disposing);
    }
}
