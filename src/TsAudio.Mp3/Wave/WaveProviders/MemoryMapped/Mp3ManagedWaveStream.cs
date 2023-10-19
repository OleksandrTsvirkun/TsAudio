using System.Threading.Tasks;
using System.Threading;

using TsAudio.Utils.Threading;
using System.Diagnostics;

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
        var bufferSize = 1152 * this.waveFormat.BitsPerSample / 8 * this.waveFormat.Channels * 2;
        this.waveProvider = new BufferedWaveProvider(this.mp3WaveFormat, bufferSize);
        this.parsing = args.Analyzing;
        this.waitForParse = args.ParseWait;
        this.decodeCts = new();
        this.decoding = this.DecodeAsync().ContinueWith(x =>
        {
            if(x.IsFaulted)
            {
                Debug.WriteLine(x.Exception?.Message);
            }
        });
    }

    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        if(cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return  Task.CompletedTask;
    }

    protected override async ValueTask DecodeExtraWaitAsync(CancellationToken cancellationToken = default)
    {
        if(this.parsing.IsCompleted)
        {
            await this.waveProvider.FlushAsync(cancellationToken);
            await this.waitForDecoding.ResetAndGetAwaiterWithCancellation(cancellationToken);
        }
        else
        {
            await this.waitForParse.ResetAndGetAwaiterWithCancellation(cancellationToken);
        }
    }
}