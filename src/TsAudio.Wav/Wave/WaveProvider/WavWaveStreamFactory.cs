using System.Buffers;
using System.Text;

using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wav.Wave.WaveProvider;

public struct DataChunckInfo
{
    public long Position { get; set; }

    public long Length { get; set; }
}

/// <summary>
/// Holds information about a RIFF file chunk
/// </summary>
public struct RiffChunk
{
    /// <summary>
    /// The chunk identifier
    /// </summary>
    public int Identifier { get; }

    /// <summary>
    /// The chunk identifier converted to a string
    /// </summary>
    public string IdentifierAsString => Encoding.UTF8.GetString(BitConverter.GetBytes(this.Identifier));

    /// <summary>
    /// The chunk length
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The stream position this chunk is located at
    /// </summary>
    public long StreamPosition { get; }

    /// <summary>
    /// Creates a RiffChunk object
    /// </summary>
    public RiffChunk(int identifier, int length, long streamPosition)
    {
        this.Identifier = identifier;
        this.Length = length;
        this.StreamPosition = streamPosition;
    }
}

public class WavWaveStreamFactory : IWaveStreamFactory
{
    private readonly IStreamManager bufferedStreamManager;
    private readonly ManualResetEventSlim consumeWaiter;
    private Task? analyzing;
    private readonly IList<RiffChunk> indices;

    private long dataChunkPosition;
    private WaveFormat waveFormat;
    private long dataChunkLength;
    private bool isRf64;

    private CancellationTokenSource? cts;

    public long SampleCount => throw new NotImplementedException();

    public long? TotalSamples => throw new NotImplementedException();

    public WaveFormat WaveFormat => this.waveFormat ?? throw new Exception("Must call init first.");

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public ValueTask<IWaveStream> GetWaveStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async ValueTask InitAsync(CancellationToken cancellationTokenExternal = default)
    {
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);

        var cancellationToken = this.cts.Token;

        var indicesReader = await this.bufferedStreamManager.GetStreamAsync(cancellationToken: cancellationToken);

        await this.InitChuckDataAsync(indicesReader, cancellationToken);
    }

    private async ValueTask InitChuckDataAsync(Stream indicesReader, CancellationToken cancellationToken)
    {
        var (isRf64, riffSize) = await this.ReadRiffHeader(indicesReader, cancellationToken);   //4

        if(isRf64)
        {
            await this.ReadDs64Chunk(indicesReader, cancellationToken);            //4+4+8+8+8
        }

        var dataChunkId = Encoding.UTF8.GetBytes("data");
        var formatChunkId = Encoding.UTF8.GetBytes("fmt ");

        DataChunckInfo dataChunkInfo = default;
        WaveFormat waveFormat = null;

        using var buffer = MemoryPool<byte>.Shared.Rent(8);

        while(indicesReader.Position <= riffSize)
        {
            await indicesReader.ReadAsync(buffer.Memory.Slice(0, 8), cancellationToken);

            var chunkIdentifier = buffer.Memory.Slice(0, 4);

            var chunkLength = BitConverter.ToInt32(buffer.Memory.Slice(0, 4).Span);

            if(chunkIdentifier.Span.SequenceEqual(dataChunkId))
            {
                dataChunkInfo = GetChunkData(indicesReader, isRf64, chunkLength);
            }
            else if(chunkIdentifier.Span.SequenceEqual(formatChunkId))
            {
                if(chunkLength > int.MaxValue)
                {
                    throw new InvalidDataException($"Format chunk length must be between 0 and {int.MaxValue}.");
                }
                waveFormat = await WaveFormatExtraData.FromFormatChunk(indicesReader, chunkLength, cancellationToken);
            }
            else
            {
                break;
            }

            await indicesReader.ReadAsync(buffer.Memory.Slice(0, 2), cancellationToken);

            var peekedChar = BitConverter.ToChar(buffer.Memory.Slice(0, 2).Span);

            if(((chunkLength % 2) != 0) && (peekedChar == 0))
            {
                indicesReader.Seek(-1, SeekOrigin.Begin);
            }
            else
            {
                indicesReader.Seek(-2, SeekOrigin.Begin);
            }
        }

        this.dataChunkLength = dataChunkInfo.Length;
        this.dataChunkPosition = dataChunkInfo.Position;
        this.waveFormat = waveFormat;

        this.analyzing = this.ParseAsync(indicesReader, riffSize, cancellationToken);
    }

    private Task ParseAsync(Stream stream, long stopPosition, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            var dataChunkId = Encoding.UTF8.GetBytes("data");
            var formatChunkId = Encoding.UTF8.GetBytes("fmt ");

            using var buffer = MemoryPool<byte>.Shared.Rent(8);

            while(stream.Position <= stopPosition)
            {
                await stream.ReadAsync(buffer.Memory.Slice(0, 8), cancellationToken);
                var chunkIdentifier = buffer.Memory.Slice(0, 4);

                var chunkLength = BitConverter.ToInt32(buffer.Memory.Slice(0, 4).Span);

                if(!chunkIdentifier.Span.SequenceEqual(dataChunkId)
                    && !chunkIdentifier.Span.SequenceEqual(formatChunkId))
                {
                    var chunkIdentifierInt = BitConverter.ToInt32(chunkIdentifier.Span);
                    var riffChunk = new RiffChunk(chunkIdentifierInt, chunkLength, stream.Position);
                    this.indices.Add(riffChunk);
                }
                await stream.ReadAsync(buffer.Memory.Slice(0, 2), cancellationToken);

                var peekedChar = BitConverter.ToChar(buffer.Memory.Slice(0, 2).Span);

                if(((chunkLength % 2) != 0) && (peekedChar == 0))
                {
                    stream.Seek(-1, SeekOrigin.Begin);
                }
                else
                {
                    stream.Seek(-2, SeekOrigin.Begin);
                }
            }
        }, cancellationToken);
    }

    private static DataChunckInfo GetChunkData(Stream indicesReader, bool isRf64, int chunkLength)
    {
        var dataChunkPosition = indicesReader.Position;
        int dataChunkLength = 0;
        if(!isRf64) // we already know the dataChunkLength if this is an RF64 file
        {
            dataChunkLength = chunkLength;
        }
        indicesReader.Position += chunkLength;
        return new DataChunckInfo() 
        { 
            Position = dataChunkPosition,
            Length = dataChunkLength,
        };
    }

    private async ValueTask<(bool IsRf64, int RiffSize)> ReadRiffHeader(Stream reader, CancellationToken cancellationToken = default)
    {
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(24);
        var buffer = bufferOwner.Memory.Slice(0, 24);

        await reader.ReadAsync(buffer, cancellationToken);

        var isRf64 = false;

        if(buffer.Span.Slice(0, 4).SequenceEqual(Encoding.UTF8.GetBytes("RF64")))
        {
            isRf64 = true;
        }
        else if(!buffer.Span.SequenceEqual(Encoding.UTF8.GetBytes("RIFF")))
        {
            throw new FormatException("Not a WAVE file - no RIFF header");
        }

        var riffSize = BitConverter.ToInt32(buffer.Span.Slice(4,4));  //4

        if (buffer.Span.Slice(8, 4).SequenceEqual(Encoding.UTF8.GetBytes("WAVE")))
        {
            throw new FormatException("Not a WAVE file - no WAVE header");
        }

        return (isRf64, riffSize);
    }


    /// <summary>
    /// http://tech.ebu.ch/docs/tech/tech3306-2009.pdf
    /// </summary>
    private async ValueTask ReadDs64Chunk(Stream reader, CancellationToken cancellationToken = default)
    {
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(32);
        var buffer = bufferOwner.Memory.Slice(0, 32);

        await reader.ReadAsync(buffer, cancellationToken);

        CheckDs64Chunck(buffer.Slice(0, 4).Span);

        int chunkSize = BitConverter.ToInt32(buffer.Span.Slice(4,4));

        var riffSize = BitConverter.ToInt64(buffer.Span.Slice(8, 8));
        var dataChunkLength = BitConverter.ToInt64(buffer.Span.Slice(16, 8));
        var sampleCount = BitConverter.ToInt64(buffer.Span.Slice(24, 8));

        reader.Seek(chunkSize - 24, SeekOrigin.Current);

        static void CheckDs64Chunck(ReadOnlySpan<byte> buffer)
        {
            var ds64ChunkId = Encoding.UTF8.GetBytes("ds64");

            if(!buffer.Slice(0, 4).SequenceEqual(ds64ChunkId))
            {
                throw new FormatException("Invalid RF64 WAV file - No ds64 chunk found");
            }
        }
    }

}
