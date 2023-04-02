using System;
using System.Runtime.InteropServices;
using System.Threading;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Wave.WaveInputs;
using TsAudio.Wave.WaveOutputs;
using TsAudio.Wave.WaveFormats;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TsAudio.Drivers.WinMM;

/// <summary>
/// Recording using waveIn api with event callbacks.
/// Use this for recording in non-gui applications
/// Events are raised as recorded buffers are made available
/// </summary>
public class WaveInEvent : IWaveIn
{
    /// <summary>
    /// Returns the number of Wave In devices available in the system
    /// </summary>
    public static int DeviceCount => WaveInterop.waveInGetNumDevs();

    /// <summary>
    /// Retrieves the capabilities of a waveIn device
    /// </summary>
    /// <param name="devNumber">Device to test</param>
    /// <returns>The WaveIn device capabilities</returns>
    public static WaveInCapabilities GetCapabilities(int devNumber)  
    {
        WaveInteropExtensions.WaveInGetDevCaps((IntPtr)devNumber, out var waveInCaps);
        return waveInCaps;
    }

    private readonly AutoResetEvent callbackEvent;
    private readonly SynchronizationContext syncContext;
    private readonly object waveInLock;
    private IntPtr waveInHandle;
    private volatile CaptureState captureState;
    private WaveInBuffer[] buffers;


    /// <summary>
    /// Milliseconds for the buffer. Recommended value is 100ms
    /// </summary>
    public int BufferMilliseconds { get; set; }

    /// <summary>
    /// Number of Buffers to use (usually 2 or 3)
    /// </summary>
    public int NumberOfBuffers { get; set; }

    /// <summary>
    /// The device number to use
    /// </summary>
    public int DeviceNumber { get; set; } = -1;

    /// <summary>
    /// WaveFormat we are recording in
    /// </summary>
    public WaveFormat WaveFormat { get; set; }

    /// <summary>
    /// Indicates recorded data is available 
    /// </summary>
    public event EventHandler<WaveInEventArgs> DataAvailable;

    /// <summary>
    /// Indicates that all recorded data has now been received.
    /// </summary>
    public event EventHandler<CaptureStateEventArgs> CaptureStateChanged;

    public CaptureState CaptureState
    {
        get => this.captureState;
        private set
        {
            this.captureState = value;
            this.CaptureStateChanged?.Invoke(this, new CaptureStateEventArgs(value), this.syncContext);
        }
    }

    /// <summary>
    /// Prepares a Wave input device for recording
    /// </summary>
    public WaveInEvent(WaveFormat waveFormat = null)
    {
        this.syncContext = GetSynchronizationContext();
        this.callbackEvent = new AutoResetEvent(false);
        this.WaveFormat = waveFormat ?? new WaveFormat(8000, 16, 1);
        this.BufferMilliseconds = 100;
        this.NumberOfBuffers = 3;
        this.captureState = CaptureState.Stopped;
    }

    /// <summary>
    /// Finalizer. Only called when user forgets to call <see>Dispose</see>
    /// </summary>
    ~WaveInEvent()
    {
        this.Dispose(false);
        Debug.Assert(false, "WaveInEvent device was not closed");
    }

    /// <summary>
    /// Dispose pattern
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if(disposing)
        {
            if(this.captureState != CaptureState.Stopped)
            {
                this.StopRecording();
            }
        }

        this.CloseWaveInDevice();
    }

    /// <summary>
    /// Dispose method
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void OpenWaveInDevice()
    {
        this.CloseWaveInDevice();

        MmResult result = WaveInterop.waveInOpenWindow(out this.waveInHandle, (IntPtr)this.DeviceNumber, this.WaveFormat,
            this.callbackEvent.SafeWaitHandle.DangerousGetHandle(), 
            IntPtr.Zero, WaveInOutOpenFlags.CallbackEvent);

        MmException.Try(result, nameof(WaveInterop.waveInOpen));

        this.CreateBuffers();
    }

    /// <summary>
    /// Start recording
    /// </summary>
    public void StartRecording()
    {
        if (this.captureState != CaptureState.Stopped)
        {
            throw new InvalidOperationException("Already recording");
        }   

        this.OpenWaveInDevice();

        WaveInteropExtensions.WaveInStart(this.waveInHandle, this.waveInLock);

        this.captureState = CaptureState.Starting;

        Task.Run(this.DoRecord);
    }

    private void DoRecord()
    {
        try
        {
            this.DoRecording();
        }
        catch (Exception e)
        {

        }
        finally
        {
            this.CaptureState = CaptureState.Stopped;
        }
    }

    /// <summary>
    /// Stop recording
    /// </summary>
    public void StopRecording()
    {
        if (this.captureState != CaptureState.Stopped)
        {
            this.CaptureState = CaptureState.Stopping;

            WaveInteropExtensions.WaveInStop(this.waveInHandle, this.waveInLock);
            //Reset, triggering the buffers to be returned
            WaveInteropExtensions.WaveInReset(this.waveInHandle, this.waveInLock);

            this.callbackEvent.Set(); // signal the thread to exit
        }
    }

    /// <summary>
    /// Gets the current position in bytes from the wave input device.
    /// it calls directly into waveInGetPosition)
    /// </summary>
    /// <returns>Position in bytes</returns>
    public long GetPosition()
    {
        WaveInteropExtensions.WaveInGetPosition(this.waveInHandle, out var mmTime, this.waveInLock);
        return mmTime.cb;
    }

    private void CloseWaveInDevice()
    {
        lock(this.waveInLock)
        {
            if (this.waveInHandle != IntPtr.Zero)
            {
                // Some drivers need the reset to properly release buffers
                WaveInterop.waveInReset(this.waveInHandle);
                this.DisposeBuffers();
                WaveInterop.waveInClose(this.waveInHandle);
                this.waveInHandle = IntPtr.Zero;
            }
        }
    }

    private void DisposeBuffers()
    {
        lock(this.waveInLock)
        {
            if(this.buffers != null)
            {
                foreach(var buffer in this.buffers)
                {
                    buffer.Dispose();
                }
                this.buffers = null;
            }
        }
    }

    private void CreateBuffers()
    {
        // Default to three buffers of 100ms each
        int bufferSize = this.BufferMilliseconds * this.WaveFormat.AverageBytesPerSecond / 1000;
        if(bufferSize % this.WaveFormat.BlockAlign != 0)
        {
            bufferSize -= bufferSize % this.WaveFormat.BlockAlign;
        }

        this.buffers = new WaveInBuffer[this.NumberOfBuffers];
        for(int n = 0; n < this.buffers.Length; n++)
        {
            this.buffers[n] = new WaveInBuffer(this.waveInHandle, bufferSize);
        }
    }

    private void DoRecording()
    {
        this.CaptureState = CaptureState.Capturing;

        foreach(var buffer in this.buffers)
        {
            if(!buffer.InQueue)
            {
                buffer.Reuse();
            }
        }

        while(this.captureState == CaptureState.Capturing)
        {
            if(this.callbackEvent.WaitOne())
            {
                // requeue any buffers returned to us
                foreach(var buffer in this.buffers)
                {
                    if(buffer.Done)
                    {
                        if(buffer.Data.Length > 0)
                        {
                            this.DataAvailable?.Invoke(this, new WaveInEventArgs(buffer.Data));
                        }

                        if(this.captureState == CaptureState.Capturing)
                        {
                            buffer.Reuse();
                        }
                    }
                }
            }
        }
    }

    private static SynchronizationContext GetSynchronizationContext()
    {
        var syncContext = SynchronizationContext.Current;
        if(syncContext != null &&
            (syncContext.GetType().Name == "LegacyAspNetSynchronizationContext" ||
            syncContext.GetType().Name == "AspNetSynchronizationContext"))
        {
            syncContext = null;
        }

        return syncContext;
    }
}

