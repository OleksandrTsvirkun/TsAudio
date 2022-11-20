using System;
using System.Drawing;
using System.Reflection;
using System.Threading;

namespace TsAudio.Decoders.Mp3
{
    internal abstract class FrameBase
    {
        private static int totalAllocation = 0;

        internal static int TotalAllocation => Interlocked.CompareExchange(ref totalAllocation, 0, 0);

        internal long Offset { get; private set; }

        internal int Length { get; set; }

        private MpegStreamReader reader;

        private byte[] savedBuffer;

        protected FrameBase() { }

        internal bool Validate(long offset, MpegStreamReader reader)
        {
            this.Offset = offset;
            this.reader = reader;

            var len = this.Validate();

            if (len > 0)
            {
                this.Length = len;
                return true;
            }

            return false;
        }

        internal void SaveBuffer()
        {
            this.savedBuffer = new byte[this.Length];
            this.reader.Read(this.Offset, this.savedBuffer.AsSpan(0, this.Length));
            Interlocked.Add(ref totalAllocation, this.Length);
        }

        internal void ClearBuffer()
        {
            Interlocked.Add(ref totalAllocation, -this.Length);
            this.savedBuffer = null;
        }

        /// <summary>
        /// Called when the stream is not "seek-able"
        /// </summary>
        internal virtual void Parse() { }


        /// <summary>
        /// Called to validate the frame header
        /// </summary>
        /// <returns>The length of the frame, or -1 if frame is invalid</returns>
        protected abstract int Validate();


        protected int Read(int offset, Span<byte> buffer)
        {
            if (this.savedBuffer is null)
            {
                return this.reader.Read(this.Offset + offset, buffer);
            }

            if(offset < 0 || offset >= this.savedBuffer.Length)
            {
                return 0;  // check against saved buffer
            }

            if(offset + buffer.Length > this.savedBuffer.Length)
            {
                buffer = buffer.Slice(0, this.savedBuffer.Length);// twiddle the size as needed
            }

            this.savedBuffer.AsSpan(offset, buffer.Length).CopyTo(buffer);

            return buffer.Length;
        }

        protected int ReadByte(int offset)
        {
            if(this.savedBuffer != null)
            {
                if(offset < 0)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if(offset >= this.savedBuffer.Length)
                {
                    return -1;
                }

                return this.savedBuffer[offset];
            }
            else
            {
                return this.reader.ReadByte(Offset + offset);
            }
        }
    }
}
