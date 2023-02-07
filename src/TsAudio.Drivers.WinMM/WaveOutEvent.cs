using System;
using System.Diagnostics;
using System.Threading;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Wave.WaveOutputs;
using TsAudio.Wave.WaveFormats;
using System.Runtime.InteropServices;
using TsAudio.Wave.WaveProviders;
using System.Threading.Tasks;

namespace TsAudio.Drivers.WinMM
{
    /// <summary>
    /// Alternative WaveOut class, making use of the Event callback
    /// </summary>
    public class WaveOutEvent : IWavePlayer, IWavePosition
    {
        private readonly object waveOutLock;
        private readonly SynchronizationContext syncContext;
        private IntPtr hWaveOut;
        private WaveOutBuffer[] buffers;
        private IWaveProvider waveProvider;
        private AutoResetEvent callbackEvent;
        private CancellationTokenSource cancellationTokenSource;


        private PlaybackState playbackState;

        public event EventHandler<PlaybackStateArgs> PlaybackStateChanged;

        public PlaybackState PlaybackState
        {
            get => this.playbackState;
            set
            {
                this.playbackState = value;
                var handler = this.PlaybackStateChanged;
                if (this.syncContext is null)
                {
                    handler?.Invoke(this, new PlaybackStateArgs(value));
                }
                else
                {
                    this.syncContext.Post(state => handler?.Invoke(this, new PlaybackStateArgs(value)), null);
                }
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
        public WaveFormat WaveFormat => waveProvider.WaveFormat;

        /// <summary>
        /// Volume for this device 1.0 is full scale
        /// </summary>
        public float Volume
        {
            get
            {
                int stereoVolume;
                MmResult result;

                lock(waveOutLock)
                {
                    result = WaveInterop.waveOutGetVolume(hWaveOut, out stereoVolume);
                }

                MmException.Try(result, nameof(WaveInterop.waveOutGetVolume));

                return (stereoVolume & 0xFFFF) / (float)0xFFFF;
            }
            set
            {
                value = Math.Max(0, Math.Min(value, 1));

                float left = value;
                float right = value;

                int stereoVolume = (int)(left * 0xFFFF) + ((int)(right * 0xFFFF) << 16);

                MmResult result;

                lock(waveOutLock)
                {
                    result = WaveInterop.waveOutSetVolume(hWaveOut, stereoVolume);
                }

                MmException.Try(result, nameof(WaveInterop.waveOutSetVolume));
            }
        }

        public long Position => this.GetPosition();

        /// <summary>
        /// Opens a WaveOut device
        /// </summary>
        public WaveOutEvent()
        {
            this.syncContext = SynchronizationContext.Current;
            if(this.syncContext is not null &&
                ((this.syncContext.GetType().Name == "LegacyAspNetSynchronizationContext") ||
                (this.syncContext.GetType().Name == "AspNetSynchronizationContext")))
            {
                this.syncContext = null;
            }

            this.waveOutLock = new object();
            this.playbackState = PlaybackState.Stopped;
            this.cancellationTokenSource = new CancellationTokenSource();

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

                if(this.PlaybackState != PlaybackState.Stopped)
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
                int bufferSize = waveProvider.WaveFormat.ConvertLatencyToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

                var result = WaveInterop.waveOutOpenWindow(
                        out hWaveOut,
                        (IntPtr)DeviceNumber,
                        this.waveProvider.WaveFormat,
                        hCallbackEvent,
                        IntPtr.Zero,
                        WaveInOutOpenFlags.CallbackEvent);

                MmException.Try(result, nameof(WaveInterop.waveOutOpenWindow));

                this.buffers = new WaveOutBuffer[NumberOfBuffers];
                this.PlaybackState = PlaybackState.Stopped;

                for(var n = 0; n < this.NumberOfBuffers; n++)
                {
                    this.buffers[n] = new WaveOutBuffer(this.hWaveOut, bufferSize, this.waveProvider, this.waveOutLock);
                }
            }
        }

        /// <summary>
        /// Start playing the audio from the WaveStream
        /// </summary>
        public void Play()
        {
            if(this.buffers == null || this.waveProvider == null)
            {
                throw new InvalidOperationException("Must call Init first");
            }

            lock(this.waveOutLock)
            {
                if(this.PlaybackState == PlaybackState.Stopped)
                {
                    this.callbackEvent.Set(); // give the thread a kick
                    this.RenewCancelationToken();
                    var cancellationToken = this.cancellationTokenSource.Token;

                    this.PlaybackState = PlaybackState.Playing;
                    Task.Factory.StartNew(async() =>
                    {
                        try
                        {
                            await this.DoPlayback(cancellationToken);
                        }
                        catch(Exception)
                        {
                        }
                        finally
                        {
                            this.PlaybackState = PlaybackState.Stopped;
                        }
                    }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                }
                else if(this.PlaybackState == PlaybackState.Paused)
                {
                    this.Resume();
                    this.callbackEvent.Set(); // give the thread a kick
                }
            }
        }

        private void RenewCancelationToken()
        {
            this.cancellationTokenSource?.Cancel();
            this.cancellationTokenSource?.Dispose();
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        private async ValueTask DoPlayback(CancellationToken cancellationToken = default)
        {
            while(this.PlaybackState != PlaybackState.Stopped)
            {
                this.callbackEvent.WaitOne();

                // requeue any buffers returned to us
                if(this.PlaybackState == PlaybackState.Playing)
                {
                    int queued = 0;
                    foreach(var buffer in this.buffers)
                    {
                        if(buffer.InQueue ||  await buffer.OnDoneAsync(cancellationToken))
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
        }

        /// <summary>
        /// Pause the audio
        /// </summary>
        public void Pause()
        {
            lock(this.waveOutLock)
            {
                if(this.PlaybackState == PlaybackState.Playing)
                {
                    this.PlaybackState = PlaybackState.Paused;

                    var result = WaveInterop.waveOutPause(this.hWaveOut);

                    MmException.Try(result, nameof(WaveInterop.waveOutPause));
                }
            }
        }

        /// <summary>
        /// Resume playing after a pause from the same position
        /// </summary>
        private void Resume()
        {
            lock(this.waveOutLock)
            {
                if(this.PlaybackState == PlaybackState.Paused)
                {
                    var result = WaveInterop.waveOutRestart(this.hWaveOut);

                    MmException.Try(result, nameof(WaveInterop.waveOutRestart));

                    this.PlaybackState = PlaybackState.Playing;
                }
            }
        }

        /// <summary>
        /// Stop and reset the WaveOut device
        /// </summary>
        public void Stop()
        {
            lock(this.waveOutLock)
            {
                if(this.PlaybackState != PlaybackState.Stopped)
                {
                    // in the call to waveOutReset with function callbacks
                    // some drivers will block here until OnDone is called
                    // for every buffer
                    this.PlaybackState = PlaybackState.Stopped;

                    this.cancellationTokenSource?.Cancel();

                    var result = WaveInterop.waveOutReset(this.hWaveOut);

                    MmException.Try(result, nameof(WaveInterop.waveOutReset));

                    this.callbackEvent.Set(); // give the thread a kick, make sure we exit
                }
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
            var mmTime = new MmTime();
            mmTime.wType = MmTime.TIME_BYTES; // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?

            MmResult result;

            lock(this.waveOutLock)
            {
                result = WaveInterop.waveOutGetPosition(this.hWaveOut, ref mmTime, Marshal.SizeOf(mmTime));
            }

            MmException.Try(result, nameof(WaveInterop.waveOutGetPosition));

            if(mmTime.wType != MmTime.TIME_BYTES)
            {
                throw new Exception(string.Format("{3}: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType, nameof(WaveInterop.waveOutGetPosition)));
            }

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
                this.Stop();

                if(disposing)
                {
                    this.DisposeBuffers();
                }

                this.CloseWaveOut();
            }
        }

        private void CloseWaveOut()
        {
            lock(this.waveOutLock)
            {
                if(this.callbackEvent != null)
                {
                    this.callbackEvent.Close();
                    this.callbackEvent = null;
                }

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
            lock(this.waveOutLock)
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

        /// <summary>
        /// Finalizer. Only called when user forgets to call <see>Dispose</see>
        /// </summary>
        ~WaveOutEvent()
        {
            this.Dispose(false);
            Debug.Assert(false, "WaveOutEvent device was not closed");
        }

        #endregion
    }
}
