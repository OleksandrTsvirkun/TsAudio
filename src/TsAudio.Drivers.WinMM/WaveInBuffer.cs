using System;
using System.Buffers;
using System.Runtime.InteropServices;
using TsAudio.Drivers.WinMM.MmeInterop;

// ReSharper disable once CheckNamespace
namespace TsAudio.Drivers.WinMM
{
    /// <summary>
    /// A buffer of Wave samples
    /// </summary>
    public class WaveInBuffer : IDisposable
    {
        private WaveHeader header;
        private readonly int bufferSize; // allocated bytes, may not be the same as bytes read
        private readonly byte[] buffer;
        private GCHandle hBuffer;
        private IntPtr waveInHandle;
        private GCHandle hHeader; // we need to pin the header structure
        private GCHandle hThis; // for the user callback

        /// <summary>
        /// Provides access to the actual record buffer (for reading only)
        /// </summary>
        public byte[] Data => this.buffer;

        /// <summary>
        /// Indicates whether the Done flag is set on this buffer
        /// </summary>
        public bool Done => (this.header.flags & WaveHeaderFlags.Done) == WaveHeaderFlags.Done;


        /// <summary>
        /// Indicates whether the InQueue flag is set on this buffer
        /// </summary>
        public bool InQueue => (this.header.flags & WaveHeaderFlags.InQueue) == WaveHeaderFlags.InQueue;

        /// <summary>
        /// Number of bytes recorded
        /// </summary>
        public int BytesRecorded => this.header.bytesRecorded;

        /// <summary>
        /// The buffer size in bytes
        /// </summary>
        public int BufferSize => this.bufferSize;

        /// <summary>
        /// creates a new wavebuffer
        /// </summary>
        /// <param name="waveInHandle">WaveIn device to write to</param>
        /// <param name="bufferSize">Buffer size in bytes</param>
        public WaveInBuffer(IntPtr waveInHandle, int bufferSize)
        {
            this.bufferSize = bufferSize;
            this.buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            this.hBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            this.waveInHandle = waveInHandle;

            this.header = new WaveHeader();

            this.hHeader = GCHandle.Alloc(header, GCHandleType.Pinned);
            this.header.dataBuffer = hBuffer.AddrOfPinnedObject();
            this.header.bufferLength = bufferSize;
            this.header.loops = 1;
            this.hThis = GCHandle.Alloc(this);
            this.header.userData = (IntPtr)hThis;

            MmException.TryExecute(() => WaveInterop.waveInPrepareHeader(this.waveInHandle, this.header, Marshal.SizeOf(this.header)), nameof(WaveInterop.waveInPrepareHeader));

            //MmException.Try(WaveInterop.waveInAddBuffer(waveInHandle, header, Marshal.SizeOf(header)), "waveInAddBuffer");
        }

        /// <summary>
        /// Place this buffer back to record more audio
        /// </summary>
        public void Reuse()
        {
            // TEST: we might not actually need to bother unpreparing and repreparing
            //MmException.Try(WaveInterop.waveInUnprepareHeader(waveInHandle, header, Marshal.SizeOf(header)), "waveUnprepareHeader");
            //MmException.Try(WaveInterop.waveInPrepareHeader(waveInHandle, header, Marshal.SizeOf(header)), "waveInPrepareHeader");
            //System.Diagnostics.Debug.Assert(header.bytesRecorded == 0, "bytes recorded was not reset properly");
            MmException.TryExecute(() => WaveInterop.waveInAddBuffer(this.waveInHandle, this.header, Marshal.SizeOf(this.header)), nameof(WaveInterop.waveInAddBuffer));
        }

        #region Dispose Pattern

        /// <summary>
        /// Finalizer for this wave buffer
        /// </summary>
        ~WaveInBuffer()
        {
            Dispose(false);
            System.Diagnostics.Debug.Assert(true, "WaveInBuffer was not disposed");
        }

        /// <summary>
        /// Releases resources held by this WaveBuffer
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Releases resources held by this WaveBuffer
        /// </summary>
        protected void Dispose(bool disposing)
        {
            if(disposing)
            {
                // free managed resources
            }
            // free unmanaged resources
            if(this.waveInHandle != IntPtr.Zero)
            {
                WaveInterop.waveInUnprepareHeader(this.waveInHandle, this.header, Marshal.SizeOf(this.header));
                waveInHandle = IntPtr.Zero;
            }
            if(this.hHeader.IsAllocated)
            {
                this.hHeader.Free();
            }
                
            if(this.hBuffer.IsAllocated)
            {
                this.hBuffer.Free();
            }

            if(this.hThis.IsAllocated)
            {
                this.hThis.Free();
            }
        }

        #endregion

    }
}
