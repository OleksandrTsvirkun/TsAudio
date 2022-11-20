using System;

using TsAudio.Utils;

namespace TsAudio.Formats.Mp3
{
    /// <summary>
    /// Represents a Xing VBR header
    /// </summary>
    public class XingHeader
    {
        [Flags]
        enum XingHeaderOptions
        {
            Frames = 1,
            Bytes = 2,
            Toc = 4,
            VbrScale = 8
        }

        private static int[] sr_table = { 44100, 48000, 32000, 99999 };

        /// <summary>
        /// Load Xing Header
        /// </summary>
        /// <param name="frame">Frame</param>
        /// <returns>Xing Header</returns>
        public static XingHeader LoadXingHeader(Mp3Frame frame)
        {
            XingHeader xingHeader = new XingHeader();
            xingHeader.frame = frame;
            int offset = 0;

            if(frame.MpegVersion == MpegVersion.Version1)
            {
                if(frame.ChannelMode != ChannelMode.Mono)
                    offset = 32 + 4;
                else
                    offset = 17 + 4;
            }
            else if(frame.MpegVersion == MpegVersion.Version2)
            {
                if(frame.ChannelMode != ChannelMode.Mono)
                    offset = 17 + 4;
                else
                    offset = 9 + 4;
            }
            else
            {
                return null;
                // throw new FormatException("Unsupported MPEG Version");
            }

            ReadOnlySpan<byte> Xing = stackalloc byte[] { (byte)'X', (byte)'i', (byte)'n', (byte)'g' };
            ReadOnlySpan<byte> Info = stackalloc byte[] { (byte)'I', (byte)'n', (byte)'f', (byte)'o' };
            if(frame.RawData.Memory.Span.Slice(offset, 4).SequenceEqual(Xing))
            {
                xingHeader.startOffset = offset;
                offset += 4;
            }
            else if(frame.RawData.Memory.Span.Slice(offset, 4).SequenceEqual(Info))
            {
                xingHeader.startOffset = offset;
                offset += 4;
            }
            else
            {
                return null;
            }

            XingHeaderOptions flags = (XingHeaderOptions)frame.RawData.Memory.Span.Slice(offset, sizeof(int)).ToBigEndianInt32();
            offset += 4;

            if((flags & XingHeaderOptions.Frames) != 0)
            {
                xingHeader.framesOffset = offset;
                offset += 4;
            }
            if((flags & XingHeaderOptions.Bytes) != 0)
            {
                xingHeader.bytesOffset = offset;
                offset += 4;
            }
            if((flags & XingHeaderOptions.Toc) != 0)
            {
                xingHeader.tocOffset = offset;
                offset += 100;
            }
            if((flags & XingHeaderOptions.VbrScale) != 0)
            {
                xingHeader.vbrScale = frame.RawData.Memory.Span.Slice(offset, sizeof(int)).ToBigEndianInt32();
                offset += 4;
            }
            xingHeader.endOffset = offset;
            return xingHeader;
        }

        private int vbrScale = -1;
        private int startOffset;
        private int endOffset;
        
        private int tocOffset = -1;
        private int framesOffset = -1;
        private int bytesOffset = -1;
        private Mp3Frame frame;

        
        /// <summary>
        /// VBR Scale property
        /// </summary>
        public int VbrScale => this.vbrScale;

        /// <summary>
        /// The MP3 frame
        /// </summary>
        public Mp3Frame Mp3Frame => this.frame;

        /// <summary>
        /// Number of frames
        /// </summary>
        public int Frames
        {
            get
            {
                if(this.framesOffset == -1)
                {
                    return -1;
                }

                return this.frame.RawData.Memory.Span.Slice(this.framesOffset, sizeof(int)).ToBigEndianInt32();
            }
            set
            {
                if(this.framesOffset == -1)
                {
                    throw new InvalidOperationException("Frames flag is not set");
                }

                value.ToBigEndianSpan().CopyTo(this.frame.RawData.Memory.Span.Slice(this.framesOffset));
            }
        }

        /// <summary>
        /// Number of bytes
        /// </summary>
        public int Bytes
        {
            get
            {
                if(this.bytesOffset == -1)
                {
                    return -1;
                }

                return this.frame.RawData.Memory.Span.Slice(this.bytesOffset, sizeof(int)).ToBigEndianInt32();
            }
            set
            {
                if(this.framesOffset == -1)
                {
                    throw new InvalidOperationException("Bytes flag is not set");
                }

                value.ToBigEndianSpan().CopyTo(this.frame.RawData.Memory.Span.Slice(this.bytesOffset));
            }
        }

        /// <summary>
        /// Sees if a frame contains a Xing header
        /// </summary>
        private XingHeader()
        {
        }
    }
}
