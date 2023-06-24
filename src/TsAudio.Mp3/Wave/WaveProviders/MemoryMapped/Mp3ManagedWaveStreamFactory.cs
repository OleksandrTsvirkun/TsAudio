﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveProviders.MemoryMapped;

public sealed class Mp3ManagedWaveStreamFactory : IWaveStreamFactory
{
    private readonly IStreamManager bufferedStreamManager;
    private readonly IMp3FrameFactory frameFactory;
    private readonly ConcurrentBag<Mp3ManagedWaveStream> waveStreams;
    private readonly ManualResetEventSlim consumeWaiter;
    private List<Mp3Index> indices;
    private CancellationTokenSource? cts;
    private Task? analyzing;
    private Mp3WaveFormat? mp3WaveFormat;
    private long? totalSamples;

    public Task Analyzing => this.analyzing ?? throw new Exception("Must call init first.");

    public IReadOnlyList<Mp3Index> Indices => this.indices ?? throw new Exception("Must call init first.");

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

    public Mp3ManagedWaveStreamFactory(Mp3ManagedWaveStreamFactoryArgs args, IMp3FrameFactory? frameFactory = null)
    {
        this.frameFactory = frameFactory ?? Mp3FrameFactory.Instance;
        this.bufferedStreamManager = args.StreamManager;
        this.totalSamples = args.TotalSamples;
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

    private async ValueTask<Mp3FrameIndex> ReadFirstFrameAsync(IAsyncEnumerator<Mp3FrameIndex> framesEnumerator, CancellationToken cancellationToken = default)
    {
        await framesEnumerator.MoveNextAsync(cancellationToken);
        var first = framesEnumerator.Current;
        await framesEnumerator.MoveNextAsync(cancellationToken);
        var second = framesEnumerator.Current;

        if(first.Frame.SampleRate != second.Frame.SampleRate
            || first.Frame.ChannelMode != second.Frame.ChannelMode)
        {
            first = second;
        }

        var frameCount = (int)(this.totalSamples ?? 0) / first.Index.SampleCount;
        this.indices = new List<Mp3Index>(frameCount);

        if(!first.Equals(second))
        {
            this.indices.Add(first.Index);
        }

        this.indices.Add(second.Index);

        return first;
    }

    public async ValueTask<IWaveStream> GetWaveStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = await this.bufferedStreamManager.GetStreamAsync(mode, cancellationToken);

        var args = new Mp3ManagedWaveStreamArgs()
        {
            ParseWait = this.consumeWaiter,
            FrameFactory = this.frameFactory,
            Indices = this.indices,
            TotalSamples = this.TotalSamples,
            Mp3WaveFormat = this.Mp3WaveFormat,
            Analyzing = this.Analyzing,
            Reader = reader,
            BufferSize = ushort.MaxValue,
        };

        var waveProvider = new Mp3ManagedWaveStream(args);
        this.waveStreams.Add(waveProvider);
        return waveProvider;
    }

    private Task ParseAsync(IAsyncEnumerator<Mp3FrameIndex> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nameof(framesEnumerator));
        ArgumentNullException.ThrowIfNull(nameof(reader));

        return Task.Factory.StartNew(ParseAsyncImpl, cancellationToken, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning, TaskScheduler.Default);

        async Task ParseAsyncImpl()
        {
            try
            {
                while(await framesEnumerator.WithCancellation(cancellationToken).MoveNextAsync(cancellationToken))
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
        }
    }

    public void Dispose()
    {
        if(!this.cts?.IsCancellationRequested ?? false)
        {
            this.cts?.Cancel();
        }

        this.cts?.Dispose();

        (this.indices as IDisposable)?.Dispose();
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
