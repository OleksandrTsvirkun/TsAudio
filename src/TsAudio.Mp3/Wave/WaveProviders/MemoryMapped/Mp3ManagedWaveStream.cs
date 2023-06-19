using System.Threading.Tasks;
using System.Threading;

using TsAudio.Utils.Threading;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

public class Mp3ManagedWaveStream : Mp3WaveStream
{
    private readonly Task parsing;
    private readonly ManualResetEventSlim waitForParse;

    public override long? TotalSamples { get; }

    internal Mp3ManagedWaveStream(Mp3ManagedWaveStreamArgs args) : base(args.Reader, args.BufferSize, args.FrameFactory)
    {
        this.indices = args.Indices;
        this.mp3WaveFormat = args.Mp3WaveFormat;
        this.TotalSamples = args.TotalSamples;
        this.decompressor = new Mp3FrameDecompressor(this.Mp3WaveFormat);
        this.waveFormat = this.decompressor.WaveFormat;
        this.waveProvider = new BufferedWaveProvider(this.WaveFormat, ushort.MaxValue * 4);
        this.parsing = args.Analyzing;
        this.waitForParse = args.ParseWait;
        this.decodeCts = new();
        this.decoding = this.DecodeAsync();
    }

    public override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask DecodeExtraWaitAsync(CancellationToken cancellationToken = default)
    {
        if(this.parsing.IsCompleted)
        {
            await this.waveProvider.FlushAsync(cancellationToken);
            this.waitForDecoding.Reset();
            await this.waitForDecoding.WaitAsync(cancellationToken);
        }
        else
        {
            this.waitForParse.Reset();
            await this.waitForParse.WaitAsync(cancellationToken);
        }
    }
}