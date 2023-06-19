namespace TsAudio.Wav.Wave.WaveProvider;

public struct RiffHeader
{
    public bool IsRf64 { get; init; }
    public int RiffSize { get; init; }
}
