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

    public IList<Mp3Index> Indices => this.indices ?? throw new Exception();

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

        var framesEnumerator = this.frameFactory.LoadFrameIndicesAsync(indicesReader, 4096, cancellationToken).GetAsyncEnumerator(cancellationToken);

        var first = await this.ReadFirstFrameAsync(framesEnumerator, cancellationToken);    

        this.mp3WaveFormat = new Mp3WaveFormat(first.Frame.SampleRate,
                                                first.Frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                first.Frame.FrameLength,
                                                first.Frame.BitRate);

        this.analyzing = this.ParseAsync(framesEnumerator, indicesReader, cancellationToken);
    }

    private async ValueTask<(Mp3FrameHeader Frame, Mp3Index Index)> ReadFirstFrameAsync(IAsyncEnumerator<(Mp3FrameHeader Frame, Mp3Index Index)> framesEnumerator, CancellationToken cancellationToken = default)
    {
        await framesEnumerator.MoveNextAsync();
        var first = framesEnumerator.Current;
        await framesEnumerator.MoveNextAsync();
        var second = framesEnumerator.Current;

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

        return first;
    }

    private async ValueTask<(Mp3Frame Frame, Mp3Index Index)> LoadFrameAsync(IAsyncEnumerator<(Mp3Frame Frame, Mp3Index Index)> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
    {
        var hasFrames = await framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        if(framesEnumerator.Current.Frame is null)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        return framesEnumerator.Current;
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

    private Task ParseAsync(IAsyncEnumerator<(Mp3FrameHeader Frame, Mp3Index Index)> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
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
                    this.indices.Add(index.Index);
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

