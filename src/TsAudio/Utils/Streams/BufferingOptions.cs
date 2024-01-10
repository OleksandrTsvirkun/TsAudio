namespace TsAudio.Utils.Streams;

public class BufferingOptions
{
    public int PauseWriterThreshold { get; init; }

    public int ResumeWriterThreshold { get; init; }

    public int BufferSize { get; init; } = 4096;
}
