using System;

using TsAudio.Formats.Mp3;

namespace TsAudio.Decoders.Mp3;

internal class Mp3FrameWrapper : IMpegFrame
{
    private Mp3Frame frame;
    private int readOffset; 
    private int bitsRead;
    private ulong bitBucket;

    internal Mp3Frame WrappedFrame
    {
        set
        {
            this.frame = value;
            this.Reset();
        }
    }

    public int SampleRate => this.frame.SampleRate;

    public int SampleRateIndex => this.frame.RawData.Memory.Span[2] >> 2 & 3;

    public int FrameLength => this.frame.FrameLength;

    public int BitRate => this.frame.BitRate;

    public MpegVersion Version
    {
        get
        {
            switch (frame.MpegVersion)
            {
                case Formats.Mp3.MpegVersion.Version1: return MpegVersion.Version1;
                case Formats.Mp3.MpegVersion.Version2: return MpegVersion.Version2;
                case Formats.Mp3.MpegVersion.Version25: return MpegVersion.Version25;
            }
            return MpegVersion.Unknown;
        }
    }

    public MpegLayer Layer
    {
        get
        {
            switch (frame.MpegLayer)
            {
                case Formats.Mp3.MpegLayer.Layer1: return MpegLayer.LayerI;
                case Formats.Mp3.MpegLayer.Layer2: return MpegLayer.LayerII;
                case Formats.Mp3.MpegLayer.Layer3: return MpegLayer.LayerIII;
            }
            return MpegLayer.Unknown;
        }
    }

    public MpegChannelMode ChannelMode
    {
        get
        {
            switch (frame.ChannelMode)
            {
                case Formats.Mp3.ChannelMode.Stereo: return MpegChannelMode.Stereo;
                case Formats.Mp3.ChannelMode.JointStereo: return MpegChannelMode.JointStereo;
                case Formats.Mp3.ChannelMode.DualChannel: return MpegChannelMode.DualChannel;
                case Formats.Mp3.ChannelMode.Mono: return MpegChannelMode.Mono;
            }
            return (MpegChannelMode)(-1);
        }
    }

    public int ChannelModeExtension => this.frame.ChannelExtension;

    public int SampleCount => this.frame.SampleCount;

    public int BitRateIndex => this.frame.BitRateIndex;

    public bool IsCopyrighted => this.frame.Copyright;

    public bool HasCrc => this.frame.CrcPresent;

    // we assume everything is good here since NAudio should've already caught any errors
    public bool IsCorrupted => false;


    public void Reset()
    {
        this.readOffset = (this.frame.CrcPresent ? 2 : 0) + 4;
        this.bitsRead = 0;
    }

    public int ReadBits(int bitCount)
    {
        if (bitCount < 1 || bitCount > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount));
        }

        while (this.bitsRead < bitCount)
        {
            if(this.readOffset == this.frame.FrameLength)
            {
                throw new System.IO.EndOfStreamException();
            }

            var b = this.frame.RawData.Memory.Span[this.readOffset++];
            this.bitBucket <<= 8;
            this.bitBucket |= (byte)(b & 0xFF);
            this.bitsRead += 8;
        }

        var temp = (int)(this.bitBucket >> this.bitsRead - bitCount & (1UL << bitCount) - 1);

        this.bitsRead -= bitCount;

        return temp;
    }
}
