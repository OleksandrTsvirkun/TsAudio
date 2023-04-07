using Collections.Pooled;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveProviders;

public sealed class Mp3WaveStreamFactory : IWaveStreamFactory
{
    private readonly IStreamManager bufferedStreamManager;
    private readonly IMp3FrameFactory frameFactory;
    private readonly PooledList<Mp3Index> indices;
    private readonly ConcurrentBag<Mp3WaveStream> waveStreams;
    private readonly ManualResetEventSlim consumeWaiter;
    private CancellationTokenSource? cts;
    private Task? analyzing;
    private Mp3WaveFormat? mp3WaveFormat;
    private long? totalSamples;

    public Task Analyzing => this.analyzing ?? throw new Exception("Must call init first.");

    public Mp3WaveFormat Mp3WaveFormat => this.mp3WaveFormat ?? throw new Exception("Must call init first.");

    public long SampleCount
    {
        get
        {
            var last = this.indices.Count > 0 ? this.indices[this.indices.Count - 1] : default;
            return last.SampleCount + last.SamplePosition;
        }
    }

    public long? TotalSamples => this.totalSamples.HasValue
                                        ? this.totalSamples
                                        : this.Analyzing.IsCompleted
                                            ? this.SampleCount
                                            : null;    

    public Mp3WaveStreamFactory(Mp3WaveStreamFactoryArgs args)
    {
        this.frameFactory = Mp3FrameFactory.Instance;
        this.bufferedStreamManager = args.StreamManager;
        this.totalSamples = args.TotalSamples;
        this.indices = new();
        this.waveStreams = new();
        this.consumeWaiter = new();
        this.cts = null;
    }

    public async ValueTask InitAsync(CancellationToken cancellationTokenExternal = default)
    {
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);

        var cancellationToken = this.cts.Token;

        var indicesReader = await this.bufferedStreamManager.GetStreamAsync(cancellationToken: cancellationToken);

        var framesEnumerator = this.frameFactory.LoadFrameIndicesAsync(indicesReader, cancellationToken).GetAsyncEnumerator(cancellationToken);

        var frame = await this.ReadFirstFrameAsync(framesEnumerator, cancellationToken);    

        this.mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate,
                                                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                frame.FrameLength,
                                                frame.BitRate);

        this.analyzing = this.ParseAsync(framesEnumerator, indicesReader, cancellationToken);
    }

    private async ValueTask<Mp3Frame> ReadFirstFrameAsync(IAsyncEnumerator<Mp3Index> framesEnumerator, CancellationToken cancellationToken = default)
    {
        using var framesReader = await this.bufferedStreamManager.GetStreamAsync(ReaderMode.Kick, cancellationToken);

        var first = await this.LoadFrameAsync(framesEnumerator, framesReader, cancellationToken);
        var second = await this.LoadFrameAsync(framesEnumerator, framesReader, cancellationToken);

        if(first.Frame.SampleRate != second.Frame.SampleRate
            || first.Frame.ChannelMode != second.Frame.ChannelMode)
        {
            first = second;
        }

        if(!first.Equals(second))
        {
            this.indices.Add(first.Index);
        }

        this.indices.Add(second.Index);

        return first.Frame;
    }

    private async ValueTask<(Mp3Index Index, Mp3Frame Frame)> LoadFrameAsync(IAsyncEnumerator<Mp3Index> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
    {
        var hasFrames = await framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        var firstFrameIndex = framesEnumerator.Current;
        var firstFrame = await this.frameFactory.LoadFrameAsync(reader, firstFrameIndex, cancellationToken);

        if(!firstFrame.HasValue)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        return (firstFrameIndex, firstFrame.Value);
    }

    public async ValueTask<IWaveStream> GetWaveStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = await this.bufferedStreamManager.GetStreamAsync(mode, cancellationToken);

        var args = new Mp3WaveStreamArgs()
        {
            ParseWait = this.consumeWaiter,
            FrameFactory = this.frameFactory,
            Indices = this.indices,
            TotalSamples = this.TotalSamples,
            Mp3WaveFormat = this.Mp3WaveFormat,
            Analyzing = this.Analyzing,
            Reader = reader,
        };
        var waveProvider = new Mp3WaveStream(args);

        this.waveStreams.Add(waveProvider);
        return waveProvider;
    }

    private Task ParseAsync(IAsyncEnumerator<Mp3Index> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nameof(framesEnumerator));
        ArgumentNullException.ThrowIfNull(nameof(reader));

        return Task.Run(async () =>
        {
            try
            {
                while (await framesEnumerator.WithCancellation(cancellationToken).MoveNextAsync(cancellationToken))
                {
                    var index = framesEnumerator.Current;
                    this.indices.Add(index);
                    this.consumeWaiter.Set();
                }
            }
            catch(OperationCanceledException)
            {

            }
            finally
            {
                await framesEnumerator.DisposeAsync();
                await reader.DisposeAsync();
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        if(!this.cts?.IsCancellationRequested ?? false)
        {
            this.cts?.Cancel();
        }

        this.cts?.Dispose();

        this.indices.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        this.Dispose();

        foreach(var waveStream in this.waveStreams)
        {
            await waveStream.DisposeAsync();
        }
    }
}

