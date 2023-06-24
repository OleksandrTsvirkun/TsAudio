using System.Buffers;
using System.Text;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Formats.Wav;

public class WavFormatMetadataReader : IWavFormatMetadataReader
{
    public static ReadOnlyMemory<byte> DataChunkId = Encoding.UTF8.GetBytes("data");
    public static ReadOnlyMemory<byte> FormatChunkId = Encoding.UTF8.GetBytes("fmt ");
    public static ReadOnlyMemory<byte> RF64ChunkId = Encoding.UTF8.GetBytes("RF64");
    public static ReadOnlyMemory<byte> RIFFChunkId = Encoding.UTF8.GetBytes("RIFF");
    public static ReadOnlyMemory<byte> WAVEChunkId = Encoding.UTF8.GetBytes("WAVE");
    public static ReadOnlyMemory<byte> Ds64ChunkId = Encoding.UTF8.GetBytes("ds64");

    public static WavFormatMetadataReader Instance = new();

    public async Task<WavMetadata> ReadWavFormatMetadataAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var riffHeader = await this.ReadRiffHeader(stream, cancellationToken);

        if(riffHeader.IsRf64)
        {
            await this.ReadDs64Chunk(stream, cancellationToken);
        }

        var stopPosition = Math.Min(riffHeader.RiffSize + 8, stream.Length);
        using var buffer = MemoryPool<byte>.Shared.Rent(8);

        WaveFormat waveFormat = null;
        long dataChunkPosition = 0;
        long dataChunkLength = 0;
        var riffChunks = new List<RiffChunk>();

        while(stream.Position <= stopPosition)
        {
            await stream.ReadAsync(buffer.Memory.Slice(0, 8), cancellationToken);
            var chunkIdentifier = buffer.Memory.Slice(0, 4);

            var chunkLength = BitConverter.ToInt32(buffer.Memory.Slice(4, 4).Span);

            if(chunkIdentifier.Span.SequenceEqual(FormatChunkId.Span))
            {
                if(chunkLength > int.MaxValue)
                {
                    throw new InvalidDataException($"Format chunk length must be between 0 and {int.MaxValue}.");
                }
                waveFormat = await WaveFormatExtraData.FromFormatChunk(stream, chunkLength, cancellationToken);
            }
            else if(chunkIdentifier.Span.SequenceEqual(DataChunkId.Span))
            {
                dataChunkPosition = stream.Position;

                if(!riffHeader.IsRf64) // we already know the dataChunkLength if this is an RF64 file
                {
                    dataChunkLength = chunkLength;
                }
                break;
            }
            else
            {
                var chunkIdentifierString = Encoding.UTF8.GetString(chunkIdentifier.Span);
                var riffChunk = new RiffChunk(chunkIdentifierString, stream.Position, chunkLength);
                riffChunks.Add(riffChunk);
                stream.Seek(chunkLength, SeekOrigin.Current);
            }
        }

        return new WavMetadata()
        {
            DataChunkLength = dataChunkLength,
            DataChunkPosition = dataChunkPosition,
            IsRf64 = riffHeader.IsRf64,
            RiffChunks = riffChunks,
            WaveFormat = waveFormat
        };
    }



    private async ValueTask<RiffHeader> ReadRiffHeader(Stream reader, CancellationToken cancellationToken = default)
    {
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(12);
        var buffer = bufferOwner.Memory.Slice(0, 12);

        await reader.ReadAsync(buffer, cancellationToken);

        var isRf64 = false;

        if(buffer.Span.Slice(0, 4).SequenceEqual(RF64ChunkId.Span))
        {
            isRf64 = true;
        }
        else if(!buffer.Span.Slice(0, 4).SequenceEqual(RIFFChunkId.Span))
        {
            throw new FormatException("Not a WAVE file - no RIFF header");
        }

        var riffSize = BitConverter.ToInt32(buffer.Span.Slice(4, 4));  //4

        if(!buffer.Span.Slice(8, 4).SequenceEqual(WAVEChunkId.Span))
        {
            throw new FormatException("Not a WAVE file - no WAVE header");
        }

        return new RiffHeader()
        {
            IsRf64 = isRf64,
            RiffSize = riffSize,
        };
    }


    /// <summary>
    /// http://tech.ebu.ch/docs/tech/tech3306-2009.pdf
    /// </summary>
    private async ValueTask ReadDs64Chunk(Stream reader, CancellationToken cancellationToken = default)
    {
        using var bufferOwner = MemoryPool<byte>.Shared.Rent(32);
        var buffer = bufferOwner.Memory.Slice(0, 32);

        await reader.ReadAsync(buffer, cancellationToken);

        if(!buffer.Slice(0, 4).Span.SequenceEqual(Ds64ChunkId.Span))
        {
            throw new FormatException("Invalid RF64 WAV file - No ds64 chunk found");
        }

        var chunkSize = BitConverter.ToInt32(buffer.Span.Slice(4, 4));

        var riffSize = BitConverter.ToInt64(buffer.Span.Slice(8, 8));
        var dataChunkLength = BitConverter.ToInt64(buffer.Span.Slice(16, 8));
        var sampleCount = BitConverter.ToInt64(buffer.Span.Slice(24, 8));

        reader.Seek(chunkSize - 24, SeekOrigin.Current);
    }
}
