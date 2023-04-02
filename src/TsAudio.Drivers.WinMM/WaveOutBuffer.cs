using System;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Wave.WaveProviders;
using System.Threading.Tasks;
using System.Threading;

namespace TsAudio.Drivers.WinMM;

/// <summary>
/// A buffer of Wave samples for streaming to a Wave Output device
/// </summary>
public class WaveOutBuffer : WaveBuffer
{
    private readonly IWaveProvider waveStream;

    /// <summary>
    /// Whether the header's in queue flag is set
    /// </summary>
    public bool InQueue => (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;

    /// <summary>
    /// creates a new wavebuffer
    /// </summary>
    /// <param name="hWaveOut">WaveOut device to write to</param>
    /// <param name="bufferSize">Buffer size in bytes</param>
    /// <param name="bufferFillStream">Stream to provide more data</param>
    /// <param name="waveOutLock">Lock to protect WaveOut API's from being called on >1 thread</param>
    public WaveOutBuffer(IntPtr hWaveOut, int bufferSize, IWaveProvider bufferFillStream, object waveOutLock) : base(hWaveOut, bufferSize, waveOutLock)
    {
        this.waveStream = bufferFillStream;
        WaveInteropExtensions.WaveOutPrepareHeader(hWaveOut, this.header, this.waveLock);
    }

    /// this is called by the WAVE callback and should be used to refill the buffer
    public async ValueTask<bool> OnDoneAsync(CancellationToken cancellationToken = default)
    {
        var buffer = this.buffer.AsMemory(0, this.bufferSize);
        var bytes = await this.waveStream.ReadAsync(buffer, cancellationToken);

        if (bytes == 0)
        {
            return false;
        }

        this.buffer.AsSpan(bytes).Clear();

        WaveInteropExtensions.WaveOutWrite(this.hWave, this.header, this.waveLock);

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if(this.hWave != IntPtr.Zero)
        {
            WaveInteropExtensions.WaveOutUnprepareHeader(this.hWave, this.header, this.waveLock);
            this.hWave = IntPtr.Zero;
        }

        base.Dispose(disposing);
    }
}
