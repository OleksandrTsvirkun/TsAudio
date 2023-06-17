using System;
using System.Buffers;

namespace TsAudio.Formats.Mp3;

/// <summary>
/// Represents an MP3 Frame
/// </summary>
public class Mp3FrameHeader
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


}

public class Mp3Frame : Mp3FrameHeader, IDisposable
{
    /// <summary>
    /// Raw frame data (includes header bytes)
    /// </summary>
    public IMemoryOwner<byte> RawData { get; internal set; }

    public void Dispose()
    {
        this.RawData?.Dispose();
    }
}
