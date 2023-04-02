using System;
using System.Runtime.InteropServices;
using TsAudio.Drivers.WinMM.MmeInterop;
using System.Buffers;
using System.Diagnostics;

namespace TsAudio.Drivers.WinMM;

public abstract class WaveBuffer : IDisposable
{
    protected WaveHeader header;
    protected readonly int bufferSize;
    protected readonly object waveLock;
    protected readonly byte[] buffer;
    protected IntPtr hWave;
    protected GCHandle hHeader; // we need to pin the header structure
    protected GCHandle hBuffer; // we need to pin the header structure
    protected GCHandle hThis; // for the user callback

    /// <summary>
    /// The buffer size in bytes
    /// </summary>
    public int BufferSize => this.bufferSize;

    public WaveBuffer(IntPtr hWave, int bufferSize, object waveLock)
    {
        this.hWave = hWave;
        this.waveLock = waveLock;

        this.bufferSize = bufferSize;
        this.buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        this.hBuffer = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);

        this.hThis = GCHandle.Alloc(this);

        this.header = new WaveHeader();
        this.hHeader = GCHandle.Alloc(this.header, GCHandleType.Pinned);
        this.header.dataBuffer = this.hBuffer.AddrOfPinnedObject();
        this.header.bufferLength = bufferSize;
        this.header.loops = 1;
        this.header.userData = (IntPtr)this.hThis;  
    }

 #region Dispose Pattern

    /// <summary>
    /// Finalizer for this wave buffer
    /// </summary>
    ~WaveBuffer()
    {
        this.Dispose(false);
        Debug.Assert(true, "WaveBuffer was not disposed");
    }

    /// <summary>
    /// Releases resources held by this WaveBuffer
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    /// <summary>
    /// Releases resources held by this WaveBuffer
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            // free managed resources
        }

        // free unmanaged resources
        if(this.hHeader.IsAllocated)
        {
            this.hHeader.Free();
        }

        if(this.hBuffer.IsAllocated)
        {
            this.hBuffer.Free();
            ArrayPool<byte>.Shared.Return(this.buffer);
        }

        if(this.hThis.IsAllocated)
        {
            this.hThis.Free();
        }
    }

#endregion
}
