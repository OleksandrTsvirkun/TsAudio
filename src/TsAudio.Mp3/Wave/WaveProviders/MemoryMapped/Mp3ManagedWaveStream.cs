using System.Threading.Tasks;
using System.Threading;

using TsAudio.Utils.Threading;
using System.Collections.Generic;
using TsAudio.Formats.Mp3;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

public class Mp3ManagedWaveStream : Mp3WaveStream
{
    private readonly Task parsing;
    private readonly ManualResetEventSlim waitForParse;

    protected override IWaveBuffer WaveBuffer { get; }

    protected override IReadOnlyList<Mp3Index> Indices { get; }

    protected override IMp3FrameDecompressor Decompressor { get; }

    protected override Task DecodingTask { get; }

    protected override CancellationTokenSource DecodingCancellationTokenSource { get; }

    public override Mp3WaveFormat Mp3WaveFormat { get; }

    public override WaveFormat WaveFormat { get; }

    public override long? Length { get; }

    internal Mp3ManagedWaveStream(Mp3ManagedWaveStreamArgs args) : base(args.Reader, args.BufferSize, args.FrameFactory)
    {
        this.Indices = args.Indices;
        this.Mp3WaveFormat = args.Mp3WaveFormat;
        this.Length = args.TotalSamples;
        this.Decompressor = new Mp3FrameDecompressor(this.Mp3WaveFormat);
        this.WaveFormat = this.Decompressor.WaveFormat;

        var bufferSize = 1152 * this.WaveFormat.BitsPerSample / 8 * this.WaveFormat.Channels * 2;

        this.WaveBuffer = new BufferedWaveProvider(this.WaveFormat, bufferSize);
        this.parsing = args.Analyzing;
        this.waitForParse = args.ParseWait;
        this.DecodingCancellationTokenSource = new();
        this.DecodingTask = this.DecodeAsync();
    }

    public override Task InitAsync(CancellationToken cancellationToken = default)
    {
        if(cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.CompletedTask;
    }

    protected override async ValueTask DecodeExtraWaitAsync(CancellationToken cancellationToken = default)
    {
        if(this.parsing.IsCompleted)
        {
            await this.WaveBuffer.FlushAsync(cancellationToken);
            await this.waitForDecoding.ResetAndGetAwaiterWithSoftCancellation(cancellationToken);
        }
        else
        {
            await this.waitForParse.ResetAndGetAwaiterWithSoftCancellation(cancellationToken);
        }
    }
}