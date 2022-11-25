using System;
using System.Runtime.InteropServices;
using System.Threading;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Wave.WaveInputs;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Drivers.WinMM
{
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

        private readonly AutoResetEvent callbackEvent;
        private readonly SynchronizationContext syncContext;
        private IntPtr waveInHandle;
        private volatile CaptureState captureState;
        private WaveInBuffer[] buffers;

        /// <summary>
        /// Indicates recorded data is available 
        /// </summary>
        public event EventHandler<WaveInEventArgs> DataAvailable;

        /// <summary>
        /// Indicates that all recorded data has now been received.
        /// </summary>
        public event EventHandler<StoppedEventArgs> RecordingStopped;

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
        public int DeviceNumber { get; set; }

        /// <summary>
        /// Prepares a Wave input device for recording
        /// </summary>
        public WaveInEvent()
        {
            this.callbackEvent = new AutoResetEvent(false);
            this.syncContext = SynchronizationContext.Current;
            this.DeviceNumber = 0;
            this.WaveFormat = new WaveFormat(8000, 16, 1);
            this.BufferMilliseconds = 100;
            this.NumberOfBuffers = 3;
            this.captureState = CaptureState.Stopped;
        }

        /// <summary>
        /// Retrieves the capabilities of a waveIn device
        /// </summary>
        /// <param name="devNumber">Device to test</param>
        /// <returns>The WaveIn device capabilities</returns>
        public static WaveInCapabilities GetCapabilities(int devNumber)
        {
            WaveInCapabilities caps = new WaveInCapabilities();
            MmException.TryExecute(() => WaveInterop.waveInGetDevCaps((IntPtr)devNumber, out caps, Marshal.SizeOf(caps)), nameof(WaveInterop.waveInGetDevCaps));
            return caps;
        }

        private void CreateBuffers()
        {
            // Default to three buffers of 100ms each
            int bufferSize = BufferMilliseconds * WaveFormat.AverageBytesPerSecond / 1000;
            if (bufferSize % WaveFormat.BlockAlign != 0)
            {
                bufferSize -= bufferSize % WaveFormat.BlockAlign;
            }

            this.buffers = new WaveInBuffer[this.NumberOfBuffers];
            for (int n = 0; n < this.buffers.Length; n++)
            {
                this.buffers[n] = new WaveInBuffer(this.waveInHandle, bufferSize);
            }
        }

        private void OpenWaveInDevice()
        {
            this.CloseWaveInDevice();

            var callbackHandle = this.callbackEvent.SafeWaitHandle.DangerousGetHandle();

            MmResult result = WaveInterop.waveInOpenWindow(out this.waveInHandle, 
                (IntPtr)this.DeviceNumber, 
                this.WaveFormat,
                callbackHandle, 
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

            MmException.TryExecute(() => WaveInterop.waveInStart(this.waveInHandle), nameof(WaveInterop.waveInStart));

            this.captureState = CaptureState.Starting;

            ThreadPool.QueueUserWorkItem(_ => RecordThread(), null);
        }

        private void RecordThread()
        {
            Exception exception = null;
            try
            {
                this.DoRecording();
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                this.captureState = CaptureState.Stopped;
                this.RaiseRecordingStoppedEvent(exception);
            }
        }

        private void DoRecording()
        {
            this.captureState = CaptureState.Capturing;

            foreach (var buffer in this.buffers)
            {
                if (!buffer.InQueue)
                {
                    buffer.Reuse();
                }
            }

            while (this.captureState == CaptureState.Capturing)
            {
                if (this.callbackEvent.WaitOne())
                {
                    // requeue any buffers returned to us
                    foreach (var buffer in this.buffers)
                    {
                        if (buffer.Done)
                        {
                            if (buffer.BytesRecorded > 0)
                            {
                                DataAvailable?.Invoke(this, new WaveInEventArgs(buffer.Data, buffer.BytesRecorded));
                            }

                            if (captureState == CaptureState.Capturing)
                            {
                                buffer.Reuse();
                            }
                        }
                    }
                }
            }
        }

        private void RaiseRecordingStoppedEvent(Exception e)
        {
            var handler = RecordingStopped;
            if (handler != null)
            {
                if (syncContext == null)
                {
                    handler(this, new StoppedEventArgs(e));
                }
                else
                {
                    syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
                }
            }
        }
        /// <summary>
        /// Stop recording
        /// </summary>
        public void StopRecording()
        {
            if (captureState != CaptureState.Stopped)
            {
                captureState = CaptureState.Stopping;
                MmException.Try(WaveInterop.waveInStop(waveInHandle), "waveInStop");

                //Reset, triggering the buffers to be returned
                MmException.Try(WaveInterop.waveInReset(waveInHandle), "waveInReset");

                callbackEvent.Set(); // signal the thread to exit
            }
        }

        /// <summary>
        /// Gets the current position in bytes from the wave input device.
        /// it calls directly into waveInGetPosition)
        /// </summary>
        /// <returns>Position in bytes</returns>
        public long GetPosition()
        {
            MmTime mmTime = new MmTime();
            mmTime.wType = MmTime.TIME_BYTES; // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?
            MmException.Try(WaveInterop.waveInGetPosition(waveInHandle, out mmTime, Marshal.SizeOf(mmTime)), "waveInGetPosition");

            if (mmTime.wType != MmTime.TIME_BYTES)
                throw new Exception(string.Format("waveInGetPosition: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType));

            return mmTime.cb;
        }

        /// <summary>
        /// WaveFormat we are recording in
        /// </summary>
        public WaveFormat WaveFormat { get; set; }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (captureState != CaptureState.Stopped)
                    StopRecording();

                CloseWaveInDevice();
            }
        }

        private void CloseWaveInDevice()
        {
            // Some drivers need the reset to properly release buffers
            WaveInterop.waveInReset(waveInHandle);
            if (buffers != null)
            {
                for (int n = 0; n < buffers.Length; n++)
                {
                    buffers[n].Dispose();
                }
                buffers = null;
            }
            WaveInterop.waveInClose(waveInHandle);
            waveInHandle = IntPtr.Zero;
        }

        /// <summary>
        /// Dispose method
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

