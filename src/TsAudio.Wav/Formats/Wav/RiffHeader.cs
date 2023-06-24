namespace TsAudio.Formats.Wav;

public struct RiffHeader
{
    public bool IsRf64 { get; init; }
    public int RiffSize { get; init; }
}
