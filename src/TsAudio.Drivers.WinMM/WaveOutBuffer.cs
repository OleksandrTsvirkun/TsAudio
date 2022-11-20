using System;
using System.Runtime.InteropServices;
using TsAudio.Drivers.WinMM.MmeInterop;
using TsAudio.Utils;
using System.Buffers;
using TsAudio.Wave.WaveProviders;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace TsAudio.Drivers.WinMM
{
    /// <summary>
    /// A buffer of Wave samples for streaming to a Wave Output device
    /// </summary>
    public class WaveOutBuffer : IDisposable
    {
        private WaveHeader header;
        private readonly int bufferSize;
        private readonly IWaveProvider waveStream;
        private readonly object waveOutLock;
        private readonly byte[] buffer;
        private IntPtr hWaveOut;
        private GCHandle hHeader; // we need to pin the header structure
        private GCHandle hBuffer; // we need to pin the header structure
        private GCHandle hThis; // for the user callback

        /// <summary>
        /// Whether the header's in queue flag is set
        /// </summary>
        public bool InQueue => (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;

        /// <summary>
        /// The buffer size in bytes
        /// </summary>
        public int BufferSize => this.bufferSize;

        /// <summary>
        /// creates a new wavebuffer
        /// </summary>
        /// <param name="hWaveOut">WaveOut device to write to</param>
        /// <param name="bufferSize">Buffer size in bytes</param>
        /// <param name="bufferFillStream">Stream to provide more data</param>
        /// <param name="waveOutLock">Lock to protect WaveOut API's from being called on >1 thread</param>
        public WaveOutBuffer(IntPtr hWaveOut, int bufferSize, IWaveProvider bufferFillStream, object waveOutLock)
        {
            this.bufferSize = bufferSize;
            this.buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            this.hWaveOut = hWaveOut;
            this.waveStream = bufferFillStream;
            this.waveOutLock = waveOutLock;
            this.header = new WaveHeader();
            this.hHeader = GCHandle.Alloc(this.header, GCHandleType.Pinned);
            this.hBuffer = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
            this.header.dataBuffer = this.hBuffer.AddrOfPinnedObject();
            this.header.bufferLength = bufferSize;
            this.header.loops = 1;
            this.hThis = GCHandle.Alloc(this);

            MmResult result;

            lock (waveOutLock)
            {
                result = WaveInterop.waveOutPrepareHeader(hWaveOut, header, Marshal.SizeOf(header));
            }

            MmException.Try(result, nameof(WaveInterop.waveOutPrepareHeader));
        }

        #region Dispose Pattern

        /// <summary>
        /// Finalizer for this wave buffer
        /// </summary>
        ~WaveOutBuffer()
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
        protected void Dispose(bool disposing)
        {
            if (disposing)
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

            if (this.hThis.IsAllocated)
            {
                this.hThis.Free();
            }
                
            if (this.hWaveOut != IntPtr.Zero)
            {
                MmResult result;

                lock (this.waveOutLock)
                {
                    result = WaveInterop.waveOutUnprepareHeader(this.hWaveOut, this.header, Marshal.SizeOf(this.header));
                }

                MmException.Try(result, nameof(WaveInterop.waveOutUnprepareHeader));

                this.hWaveOut = IntPtr.Zero;
            }
        }

        #endregion

        /// this is called by the WAVE callback and should be used to refill the buffer
        public async ValueTask<bool> OnDoneAsync(CancellationToken cancellationToken = default)
        {
            var bytes = await this.waveStream.ReadAsync(buffer.AsMemory(0, this.bufferSize), cancellationToken);

            if (bytes == 0)
            {
                return false;
            }

            this.buffer.AsSpan(bytes).Clear();

            WriteToWaveOut();

            return true;
        }

        private void WriteToWaveOut()
        {
            MmResult result;

            lock (this.waveOutLock)
            {
                result = WaveInterop.waveOutWrite(this.hWaveOut, this.header, Marshal.SizeOf(this.header));
            }

            MmException.Try(result, nameof(WaveInterop.waveOutWrite));
        }
    }
}
