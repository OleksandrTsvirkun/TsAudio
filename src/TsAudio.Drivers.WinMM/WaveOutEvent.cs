using System;
using System.Threading;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Wave.WaveOutputs;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;
using System.Threading.Tasks;
using System.Diagnostics;
using TsAudio.Utils.Threading;

namespace TsAudio.Drivers.WinMM;

/// <summary>
/// Alternative WaveOut class, making use of the Event callback
/// </summary>
public class WaveOutEvent : IWavePlayer, IWavePosition
{
    private readonly Lazy<SynchronizationContext> playbackSyncContextLazy;
    private readonly Lazy<TaskScheduler> playbackTaskSchedulerLazy;

    private readonly object waveOutLock;
    private readonly SynchronizationContext syncContext;

    private IntPtr hWaveOut;
    private WaveOutBuffer[] buffers;
    private IWaveProvider waveProvider;
    private AutoResetEvent callbackEvent;
    private CancellationTokenSource cts;
    private Task playing;
    private bool disposed;

    private volatile PlaybackState playbackState;

    public event EventHandler<PlaybackStateEventArgs> PlaybackStateChanged;

    public PlaybackState PlaybackState
    {
        get => this.playbackState;
        private set
        {
            this.playbackState = value;
            this.PlaybackStateChanged?.Invoke(this, new PlaybackStateEventArgs(value), this.syncContext);
        }
    }

    /// <summary>
    /// Gets or sets the desired latency in milliseconds
    /// Should be set before a call to Init
    /// </summary>
    public int DesiredLatency { get; set; }

    /// <summary>
    /// Gets or sets the number of buffers used
    /// Should be set before a call to Init
    /// </summary>
    public int NumberOfBuffers { get; set; }

    /// <summary>
    /// Gets or sets the device number
    /// Should be set before a call to Init
    /// This must be between -1 and <see>DeviceCount</see> - 1.
    /// -1 means stick to default device even default device is changed
    /// </summary>
    public int DeviceNumber { get; set; } = -1;


    /// <summary>
    /// Gets a <see cref="Wave.WaveFormat"/> instance indicating the format the hardware is using.
    /// </summary>
    public WaveFormat WaveFormat => this.waveProvider.WaveFormat;

    /// <summary>
    /// Volume for this device 1.0 is full scale
    /// </summary>
    public float Volume
    {
        get
        {
            WaveInteropExtensions.WaveOutGetVolume(this.hWaveOut, out var stereoVolume, this.waveOutLock);

            return (stereoVolume & 0xFFFF) / (float)0xFFFF;
        }
        set
        {
            value = Math.Max(0, Math.Min(value, 1));

            int stereoVolume = (int)(value * 0xFFFF) + ((int)(value * 0xFFFF) << 16);

            WaveInteropExtensions.WaveOutSetVolume(this.hWaveOut, stereoVolume, this.waveOutLock);
        }
    }

    public long Position => this.GetPosition();

    /// <summary>
    /// Opens a WaveOut device
    /// </summary>
    public WaveOutEvent()
    {
        this.playbackSyncContextLazy = new(() => new SingleThreadSynchronizationContext("WaveOutEvent Playing Thread #1", ThreadPriority.Highest));
        this.playbackTaskSchedulerLazy = new Lazy<TaskScheduler>(this.GetTaskScheduler);
        this.syncContext = GetSynchronizationContext();
        this.waveOutLock = new object();
        this.DesiredLatency = 300;
        this.NumberOfBuffers = 2;
        this.Volume = 1f;
    }

    /// <summary>
    /// Initialises the WaveOut device
    /// </summary>
    /// <param name="waveProvider">WaveProvider to play</param>
    public void Init(IWaveProvider waveProvider)
    {
        lock(this.waveOutLock)
        {
            this.Stop();

            if(this.PlaybackState != TsAudio.Wave.WaveOutputs.PlaybackState.Stopped)
            {
                throw new InvalidOperationException("Can't re-initialize during playback");
            }

            if(this.hWaveOut != IntPtr.Zero)
            {
                // normally we don't allow calling Init twice, but as experiment, see if we can clean up and go again
                // try to allow reuse of this waveOut device
                // n.b. risky if Playback thread has not exited
                this.DisposeBuffers();
                this.CloseWaveOut();
            }

            this.callbackEvent = new AutoResetEvent(false);
            this.waveProvider = waveProvider;

            var hCallbackEvent = this.callbackEvent.SafeWaitHandle.DangerousGetHandle();

            var result = WaveInterop.waveOutOpenWindow(
                    out this.hWaveOut,
                    this.DeviceNumber,
                    this.waveProvider.WaveFormat,
                    hCallbackEvent,
                    IntPtr.Zero,
                    WaveInOutOpenFlags.CallbackEvent);

            MmException.Try(result, nameof(WaveInterop.waveOutOpenWindow));

            this.PlaybackState = PlaybackState.Stopped;

            this.buffers = this.CreateBuffers();
        }
    }

    /// <summary>
    /// Start playing the audio from the WaveStream
    /// </summary>
    public void Play()
    {
        this.ThrowIfDisposed();
        this.ThrowIfNotInitialized();

        lock(this.waveOutLock)
        {
            if(this.PlaybackState == PlaybackState.Stopped)
            {
                this.PlaybackState = PlaybackState.Playing;
                this.callbackEvent.Set(); // give the thread a kick
                this.RenewCancelationToken();

                this.playing?.Dispose();

                this.playing = Task.Factory.StartNew(this.DoPlaybackWrapper, this.cts.Token, TaskCreationOptions.LongRunning, this.playbackTaskSchedulerLazy.Value);

            }
            else if(this.PlaybackState == TsAudio.Wave.WaveOutputs.PlaybackState.Paused)
            {
                this.Resume();
                this.callbackEvent.Set(); // give the thread a kick
            }
        }
    }

    private TaskScheduler GetTaskScheduler()
    {
        var syncContext = SynchronizationContext.Current;

        SynchronizationContext.SetSynchronizationContext(this.playbackSyncContextLazy.Value);

        var scheduler = TaskScheduler.FromCurrentSynchronizationContext();

        SynchronizationContext.SetSynchronizationContext(syncContext);

        return scheduler;
    }

    /// <summary>
    /// Pause the audio
    /// </summary>
    public void Pause()
    {
        this.ThrowIfDisposed();
        this.ThrowIfNotInitialized();

        if(this.PlaybackState != PlaybackState.Playing)
        {
            return;
        }

        WaveInteropExtensions.WaveOutPause(this.hWaveOut, this.waveOutLock);

        this.PlaybackState = PlaybackState.Paused;
    }

    /// <summary>
    /// Stop and reset the WaveOut device
    /// </summary>
    public void Stop()
    {
        this.ThrowIfDisposed();

        if(this.PlaybackState != PlaybackState.Stopped)
        {
            // in the call to waveOutReset with function callbacks
            // some drivers will block here until OnDone is called
            // for every buffer
            this.PlaybackState = PlaybackState.Stopped;

            this.cts?.Cancel();

            WaveInteropExtensions.WaveOutReset(this.hWaveOut, this.waveOutLock);

            this.callbackEvent.Set(); // give the thread a kick, make sure we exit
        }
    }

    /// <summary>
    /// Gets the current position in bytes from the wave output device.
    /// (n.b. this is not the same thing as the position within your reader
    /// stream - it calls directly into waveOutGetPosition)
    /// </summary>
    /// <returns>Position in bytes</returns>
    public long GetPosition()
    {
        this.ThrowIfDisposed();
        this.ThrowIfNotInitialized();

        WaveInteropExtensions.WaveOutGetPosition(this.hWaveOut, out var mmTime, this.waveOutLock);
        return mmTime.cb;
    }

    #region Dispose Pattern

    /// <summary>
    /// Closes this WaveOut device
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    /// <summary>
    /// Closes the WaveOut device and disposes of buffers
    /// </summary>
    /// <param name="disposing">True if called from <see>Dispose</see></param>
    protected void Dispose(bool disposing)
    {
        lock(this.waveOutLock)
        {
            if(!this.disposed)
            {
                this.Stop();
                this.DisposeBuffers();
                this.CloseWaveOut();
                this.disposed = true;
            }

        }
    }

    private void CloseWaveOut()
    {
        lock(this.waveOutLock)
        {
            this.callbackEvent?.Close();
            this.callbackEvent = null;

            if(this.hWaveOut != IntPtr.Zero)
            {
                var result = WaveInterop.waveOutClose(this.hWaveOut);
                MmException.Try(result, nameof(WaveInterop.waveOutClose));
                this.hWaveOut = IntPtr.Zero;
            }
        }
    }

    private void DisposeBuffers()
    {
        if(this.hWaveOut != IntPtr.Zero)
        {
            WaveInteropExtensions.WaveOutReset(this.hWaveOut, this.waveOutLock);
        }

        lock(this.waveOutLock)
        {
            if(this.buffers is not null)
            {
                foreach(var buffer in this.buffers)
                {
                    buffer.Dispose();
                }
                this.buffers = null;
            }
        }
    }

    /// <summary>
    /// Finalizer. Only called when user forgets to call <see>Dispose</see>
    /// </summary>
    ~WaveOutEvent()
    {
        this.Dispose(false);
        Debug.Assert(false, "WaveOutEvent device was not closed.");
    }

    #endregion

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

    private void RenewCancelationToken()
    {
        if(this.cts is not null)
        {
            if(!this.cts.IsCancellationRequested)
            {
                this.cts.Cancel();
            }
            this.cts.Dispose();
        }

        this.cts = new CancellationTokenSource();

        this.cts.Token.Register(() => this.callbackEvent.Set());
    }

    private async ValueTask DoPlayback(CancellationToken cancellationToken = default)
    {
        while(this.PlaybackState != PlaybackState.Stopped 
            && !cancellationToken.IsCancellationRequested
            && this.callbackEvent.WaitOne())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if(this.PlaybackState != PlaybackState.Playing)
            {
                continue;
            }

            int queued = 0;
            foreach(var buffer in this.buffers)
            {
                if(buffer.InQueue || await buffer.OnDoneAsync(cancellationToken))
                {
                    queued++;
                }
            }

            if(queued == 0)
            {
                this.PlaybackState = PlaybackState.Stopped;
                this.callbackEvent?.Set();
            }
        }
    }

    /// <summary>
    /// Resume playing after a pause from the same position
    /// </summary>
    private void Resume()
    {
        if(this.PlaybackState != PlaybackState.Paused)
        {
            return;
        }

        WaveInteropExtensions.WaveOutRestart(this.hWaveOut, this.waveOutLock);

        this.PlaybackState = PlaybackState.Playing;
    }


    private WaveOutBuffer[] CreateBuffers()
    {
        var bufferSize = this.waveProvider.WaveFormat.ConvertLatencyToByteSize((this.DesiredLatency + this.NumberOfBuffers - 1) / this.NumberOfBuffers);

        var buffers = new WaveOutBuffer[this.NumberOfBuffers];

        for(var n = 0; n < this.NumberOfBuffers; n++)
        {
            buffers[n] = new WaveOutBuffer(this.hWaveOut, bufferSize, this.waveProvider, this.waveOutLock);
        }

        return buffers;
    }

    private async Task DoPlaybackWrapper()
    {
        try
        {
            await this.DoPlayback(this.cts.Token);
        }
        catch(Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
        finally
        {
            this.PlaybackState = PlaybackState.Stopped;
        }
    }

    private void ThrowIfNotInitialized()
    {
        if(this.buffers == null || this.waveProvider == null)
        {
            throw new InvalidOperationException("Must call Init first");
        }
    }

    private void ThrowIfDisposed()
    {
        if(this.disposed)
        {
            throw new ObjectDisposedException(this.GetType().Name);
        }
    }
}
