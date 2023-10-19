namespace TsAudio.Formats.Mp3;

public struct Mp3FrameIndex
{
    public Mp3Index Index { get; set; }

    public Mp3FrameHeader Frame { get; set; }
}
