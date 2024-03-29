﻿using System;

namespace TsAudio.Formats.Mp3;

/// <summary>
/// Represents an MP3 Frame
/// </summary>
public class Mp3FrameHeader : IEquatable<Mp3FrameHeader>
{
    public const int MaxFrameLength = 16 * 1024;
    
    /// <summary>
    /// Sample rate of this frame
    /// </summary>
    public int SampleRate { get; internal set; }

    /// <summary>
    /// Frame length in bytes
    /// </summary>
    public ushort FrameLength { get; internal set; }

    /// <summary>
    /// Bit Rate
    /// </summary>
    public int BitRate { get; internal set; }

    /// <summary>
    /// MPEG Version
    /// </summary>
    public MpegVersion MpegVersion { get; internal set; }

    /// <summary>
    /// MPEG Layer
    /// </summary>
    public MpegLayer MpegLayer { get; internal set; }

    /// <summary>
    /// Channel Mode
    /// </summary>
    public ChannelMode ChannelMode { get; internal set; }

    /// <summary>
    /// The number of samples in this frame
    /// </summary>
    public ushort SampleCount { get; internal set; }

    /// <summary>
    /// The channel extension bits
    /// </summary>
    public int ChannelExtension { get; internal set; }

    /// <summary>
    /// The bitrate index (directly from the header)
    /// </summary>
    public int BitRateIndex { get; internal set; }

    /// <summary>
    /// Whether the Copyright bit is set
    /// </summary>
    public bool Copyright { get; internal set; }

    /// <summary>
    /// Whether a CRC is present
    /// </summary>
    public bool CrcPresent { get; internal set; }

    public bool Equals(Mp3FrameHeader? other)
    {
        return other is not null
            && other.SampleRate == SampleRate
            && other.FrameLength == FrameLength
            && other.BitRate == BitRate
            && other.MpegVersion == MpegVersion
            && other.MpegLayer == MpegLayer
            && other.ChannelMode == ChannelMode
            && other.SampleCount == SampleCount
            && other.ChannelExtension == ChannelExtension
            && other.Copyright == Copyright
            && other.CrcPresent == CrcPresent;   
    }

    public override bool Equals(object? obj)
    {
        return base.Equals(obj as Mp3FrameHeader);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();

        hashCode.Add(SampleRate);
        hashCode.Add(FrameLength);
        hashCode.Add(BitRate);
        hashCode.Add(MpegVersion);
        hashCode.Add(MpegLayer);
        hashCode.Add(ChannelMode);
        hashCode.Add(SampleCount);
        hashCode.Add(ChannelExtension);
        hashCode.Add(Copyright);
        hashCode.Add(CrcPresent);

        return hashCode.ToHashCode();
    }
}
