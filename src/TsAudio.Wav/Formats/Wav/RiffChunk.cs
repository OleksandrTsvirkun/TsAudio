namespace TsAudio.Formats.Wav;

/// <summary>
/// Holds information about a RIFF file chunk
/// </summary>
public struct RiffChunk
{
    /// <summary>
    /// The chunk identifier converted to a string
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// The stream position this chunk is located at
    /// </summary>
    public long StreamPosition { get; }

    /// <summary>
    /// The chunk length
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Creates a RiffChunk object
    /// </summary>
    public RiffChunk(string identifier, long streamPosition, int length)
    {
        this.Identifier = identifier;
        this.Length = length;
        this.StreamPosition = streamPosition;
    }
}


