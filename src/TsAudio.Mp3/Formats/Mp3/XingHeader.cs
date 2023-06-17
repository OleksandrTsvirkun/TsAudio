using System;

using TsAudio.Utils;

namespace TsAudio.Formats.Mp3;

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

    private static ReadOnlyMemory<byte> Xing = new byte[] { (byte)'X', (byte)'i', (byte)'n', (byte)'g' };
    private static ReadOnlyMemory<byte> Info = new  byte[] { (byte)'I', (byte)'n', (byte)'f', (byte)'o' };

    private static int[] sr_table = { 44100, 48000, 32000, 99999 };

    /// <summary>
    /// Load Xing Header
    /// </summary>
    /// <param name="frame">Frame</param>
    /// <returns>Xing Header</returns>
    public static XingHeader? LoadXingHeader(Mp3Frame frame)
    {
        int offset = 0;

        var isMpeg = TrySetOffset(frame, ref offset);

        if(!isMpeg)
        {
            return null;
        }

        var span = frame.RawData.Memory.Span;

        var startOffset = -1;
        var hasStartOffset = TryGetStartOffset(ref offset, span, ref startOffset);

        if (!hasStartOffset)
        {
            return null;
        }

        var flags = (XingHeaderOptions)span.Slice(offset, sizeof(int)).ToBigEndianInt32();
        offset += 4;

        var framesOffset = GetFramesOffset(flags, ref offset);
        var bytesOffset = GetBytesOffset(flags, ref offset);
        var tocOffset = GetTocOffset(flags, ref offset);
        var vbrScale = GetVbrScale(flags, ref offset, span);
        var endOffset = offset;

        return new XingHeader() 
        { 
            bytesOffset = bytesOffset,
            startOffset = startOffset,
            endOffset = endOffset,
            frame = frame,
            framesOffset = framesOffset,
            tocOffset = tocOffset,
            vbrScale = vbrScale,
        };
    }

    private static bool TryGetStartOffset(ref int offset, Span<byte> span, ref int startOffset)
    {
        if(span.Slice(offset, 4).SequenceEqual(Xing.Span))
        {
            startOffset = offset;
            offset += 4;
        }
        else if(span.Slice(offset, 4).SequenceEqual(Info.Span))
        {
            startOffset = offset;
            offset += 4;
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool TrySetOffset(Mp3Frame frame, ref int offset)
    {
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
            return false;
        }

        return true;
    }

    private static int GetVbrScale(XingHeaderOptions flags, ref int offset, Span<byte> span)
    {
        var vbrScale = 0;
        if((flags & XingHeaderOptions.VbrScale) != 0)
        {
            vbrScale = span.Slice(offset, sizeof(int)).ToBigEndianInt32();
            offset += 4;
        }

        return vbrScale;
    }

    private static int GetTocOffset(XingHeaderOptions flags, ref int offset)
    {
        var tocOffset = 0;
        if((flags & XingHeaderOptions.Toc) != 0)
        {
            tocOffset = offset;
            offset += 100;
        }

        return tocOffset;
    }

    private static int GetBytesOffset( XingHeaderOptions flags, ref int offset)
    {
        var bytesOffset = 0;
        if((flags & XingHeaderOptions.Bytes) != 0)
        {
            bytesOffset = offset;
            offset += 4;
        }

        return bytesOffset;
    }

    private static int GetFramesOffset(XingHeaderOptions flags, ref int offset)
    {
        var framesOffset = 0;
        if((flags & XingHeaderOptions.Frames) != 0)
        {
            framesOffset = offset;
            offset += 4;
        }

        return framesOffset;
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
