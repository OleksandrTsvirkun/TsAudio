namespace TsAudio.Formats.Mp3;

public struct Mp3Index
{
    public long StreamPosition { get; set; }

    public int FrameLength { get; set; }

    public long SamplePosition { get; set; }

    public long SampleCount { get; set; }
}
