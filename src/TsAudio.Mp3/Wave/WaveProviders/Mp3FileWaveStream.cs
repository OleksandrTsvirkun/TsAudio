using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders;
public class Mp3FileWaveStream : Mp3WaveStream
{
    public override long? TotalSamples
    {
        get
        {
            if(this.indices.IsNullOrEmpty())
            {
                return null;
            }

            var lastIndex = this.indices.Last();
            return lastIndex.SamplePosition + lastIndex.SampleCount;
        }
    } 

    public override long Position
    {
        get
        {
            if (this.index >= this.indices.Count)
            {
                return this.TotalSamples ?? 0;
            }

            return this.indices[this.index].SamplePosition;
        }
    }

    public Mp3FileWaveStream(Stream stream, int bufferSize = ushort.MaxValue, IMp3FrameFactory? frameFactory = null) : base(stream, bufferSize, frameFactory)
    {
    }

    public async override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        this.indices = await this.frameFactory.LoadFrameIndicesAsync(this.stream, cancellationToken: cancellationToken).Select(x => x.Index).ToListAsync(cancellationToken);

        var frame = await this.frameFactory.LoadFrameAsync(this.stream, this.indices[0], cancellationToken);

        if (frame is null)
        { 
            throw new ArgumentNullException(nameof(frame));
        }

        this.mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate,
                                                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                frame.FrameLength,
                                                frame.BitRate);

        this.decompressor = new Mp3FrameDecompressor(this.mp3WaveFormat);

        this.waveFormat = this.decompressor.WaveFormat;

        this.waveProvider = new BufferedWaveProvider(this.mp3WaveFormat, this.bufferSize);

        this.decodeCts = new();
        this.decoding = this.DecodeAsync();
    }
}
