using System;

namespace TsAudio.Decoders.Mp3
{
    internal class BitReservoir
    {
        // Per the spec, the maximum buffer size for layer III is 7680 bits, which is 960 bytes.
        // The only catch is if we're decoding a "free" frame, which could be a lot more (since
        //  some encoders allow higher bitrates to maintain audio transparency).
        private byte[] buffer = new byte[8192];
        private int start = 0;
        private int end = -1;
        private int bitsLeft = 0;
        private long bitsRead = 0L;

        private static int GetSlots(IMpegFrame frame)
        {
            var cnt = frame.FrameLength - 4;
            if(frame.HasCrc)
            {
                cnt -= 2;
            }
                
            if(frame.Version == MpegVersion.Version1 && frame.ChannelMode != MpegChannelMode.Mono)
            {
                return cnt - 32;
            }
                
            if(frame.Version > MpegVersion.Version1 && frame.ChannelMode == MpegChannelMode.Mono)
            {
                return cnt - 9;
            }
                
            return cnt - 17;

        }

        public int BitsAvailable => this.bitsLeft > 0
                                    ? (this.end + this.buffer.Length - this.start) % this.buffer.Length * 8 + this.bitsLeft
                                    : 0;

        public long BitsRead => this.bitsRead;

        public bool AddBits(IMpegFrame frame, int overlap)
        {
            var originalEnd = this.end;

            var slots = GetSlots(frame);
            while(--slots >= 0)
            {
                var temp = frame.ReadBits(8);

                if(temp == -1)
                {
                    throw new System.IO.InvalidDataException("Frame did not have enough bytes!");
                }
                    
                this.buffer[++this.end] = (byte)temp;
                if(this.end == this.buffer.Length - 1)
                {
                    this.end = -1;
                }
            }

            this.bitsLeft = 8;

            if(originalEnd == -1)
            {
                // it's either the start of the stream or we've reset...  only return true if overlap says this frame is enough
                return overlap == 0;
            }
            else
            {
                // it's not the start of the stream so calculate _start based on whether we have enough bytes left

                // if we have enough bytes, reset start to match overlap
                if((originalEnd + 1 - this.start + this.buffer.Length) % this.buffer.Length >= overlap)
                {
                    this.start = (originalEnd + 1 - overlap + this.buffer.Length) % this.buffer.Length;
                    return true;
                }
                // otherwise, just set start to match the start of the frame (we probably skipped a frame)
                else
                {
                    this.start = originalEnd + overlap;
                    return false;
                }
            }
        }

        public int GetBits(int count)
        {
            int bitsRead;
            var bits = this.TryPeekBits(count, out bitsRead);

            if(bitsRead < count)
            {
                throw new System.IO.InvalidDataException("Reservoir did not have enough bytes!");
            }
                
            this.SkipBits(count);

            return bits;
        }

        public int Get1Bit()
        {
            // this is an optimized single-bit reader
            if(this.bitsLeft == 0)
            {
                throw new System.IO.InvalidDataException("Reservoir did not have enough bytes!");
            }

            --this.bitsLeft;
            ++this.bitsRead;

            var val = this.buffer[start] >> this.bitsLeft & 1;

            if(this.bitsLeft == 0 && (this.start = (this.start + 1) % this.buffer.Length) != this.end + 1)
            {
                this.bitsLeft = 8;
            }

            return val;
        }

        public int TryPeekBits(int count, out int readCount)
        {
            if(count < 0 || count > 32)
                throw new ArgumentOutOfRangeException("count", "Must return between 0 and 32 bits!");

            // if we don't have any bits left, just return no bits read
            if(this.bitsLeft == 0 || count == 0)
            {
                readCount = 0;
                return 0;
            }

            // get bits from the current start of the reservoir
            var bits = (int)this.buffer[this.start];
            if(count < this.bitsLeft)
            {
                // just grab the bits, adjust the "left" count, and return
                bits >>= this.bitsLeft - count;
                bits &= (1 << count) - 1;
                readCount = count;
                return bits;
            }

            // we have to do it the hard way...
            bits &= (1 << this.bitsLeft) - 1;
            count -= this.bitsLeft;
            readCount = this.bitsLeft;

            var resStart = this.start;

            // arg... gotta grab some more bits...
            while(count > 0)
            {
                // advance the start marker, and if we just advanced it past the end of the buffer, bail
                if((resStart = (resStart + 1) % this.buffer.Length) == this.end + 1)
                {
                    break;
                }

                // figure out how many bits to pull from it
                var bitsToRead = Math.Min(count, 8);

                // move the existing bits over
                bits <<= bitsToRead;
                bits |= buffer[resStart] >> (8 - bitsToRead) % 8;

                // update our count
                count -= bitsToRead;

                // update our remaining bits
                readCount += bitsToRead;
            }

            return bits;
        }

        public void SkipBits(int count)
        {
            if(count > 0)
            {
                // make sure we have enough bits to skip
                if(count > this.BitsAvailable)
                {
                    throw new ArgumentOutOfRangeException("count");
                }
                    
                // now calculate the new positions
                var offset = 8 - this.bitsLeft + count;
                this.start = (offset / 8 + this.start) % this.buffer.Length;
                this.bitsLeft = 8 - offset % 8;

                this.bitsRead += count;
            }
        }

        public void RewindBits(int count)
        {
            this.bitsLeft += count;
            this.bitsRead -= count;

            while(this.bitsLeft > 8)
            {
                --this.start;
                this.bitsLeft -= 8;
            }

            while(this.start < 0)
            {
                this.start += this.buffer.Length;
            }
        }

        public void FlushBits()
        {
            if(this.bitsLeft < 8)
            {
                this.SkipBits(this.bitsLeft);
            }
        }

        public void Reset()
        {
            this.start = 0;
            this.end = -1;
            this.bitsLeft = 0;
        }
    }
}
