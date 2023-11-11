using Collections.Pooled;

using Microsoft.Extensions.ObjectPool;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly bool disposeStreamManager;
    private readonly IMp3FrameFactory frameFactory;
    private readonly ConcurrentBag<Mp3ManagedWaveStream> waveStreams;
    private readonly ManualResetEventSlim consumeWaiter;
    private readonly int bufferSize;
    private PooledList<Mp3Index> indices;
    private CancellationTokenSource? cts;
    private Task? analyzing;
    private Mp3WaveFormat? mp3WaveFormat;
    private long? totalSamples;
    private bool isDisposed;
    
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
                                        ? this.totalSamples.Value
                                        : this.Analyzing.IsCompleted
                                            ? this.SampleCount
                                            : null;

    public Mp3ManagedWaveStreamFactory(Mp3ManagedWaveStreamFactoryArgs args, IMp3FrameFactory? frameFactory = null)
    {
        this.indices = new PooledList<Mp3Index>();
        this.frameFactory = frameFactory ?? Mp3FrameFactory.Instance;
        this.bufferedStreamManager = args.StreamManager;
        this.totalSamples = args.TotalSamples;
        this.waveStreams = new();
        this.consumeWaiter = new();
        this.bufferSize = args.BufferSize;
        this.disposeStreamManager = args.DisposeStreamManager;
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var indicesReader = await this.bufferedStreamManager.GetStreamAsync(cancellationToken: this.cts.Token).ConfigureAwait(false);

        var framesEnumerator = this.frameFactory.LoadFrameIndicesAsync(indicesReader, this.bufferSize, this.cts.Token).GetAsyncEnumerator(this.cts.Token);

        var first = await this.ReadFirstFrameAsync(framesEnumerator, this.cts.Token).ConfigureAwait(false);

        this.mp3WaveFormat = new Mp3WaveFormat(first.Frame.SampleRate,
                                                first.Frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                first.Frame.FrameLength,
                                                first.Frame.BitRate);

        this.analyzing = this.ParseAsync(framesEnumerator, indicesReader, this.cts.Token);
    }

    public async Task<IWaveStream> GetWaveStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = await this.bufferedStreamManager.GetStreamAsync(mode, cancellationToken).ConfigureAwait(false);

        var args = new Mp3ManagedWaveStreamArgs()
        {
            ParseWait = this.consumeWaiter,
            FrameFactory = this.frameFactory,
            Indices = this.indices,
            TotalSamples = this.TotalSamples,
            Mp3WaveFormat = this.Mp3WaveFormat,
            Analyzing = this.Analyzing,
            Reader = reader,
            BufferSize = this.bufferSize,
        };

        var waveProvider = new Mp3ManagedWaveStream(args);
        this.waveStreams.Add(waveProvider);
        return waveProvider;
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

        if(!first.Equals(second))
        {
            this.indices.Add(first.Index);
        }

        this.indices.Add(second.Index);

        return first;
    }

    private Task ParseAsync(IAsyncEnumerator<Mp3FrameIndex> framesEnumerator, Stream reader, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nameof(framesEnumerator));
        ArgumentNullException.ThrowIfNull(nameof(reader));

        return Task.Run(async () =>
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
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                await framesEnumerator.DisposeAsync();
                await reader.DisposeAsync();
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if(this.isDisposed)
        {
            return;
        }

        foreach(var waveStream in this.waveStreams)
        {
            await waveStream.DisposeAsync().ConfigureAwait(false);
        }

        if (this.cts is not null && !this.cts.IsCancellationRequested)
        {
            this.cts.Cancel();
            this.cts.Dispose();
        }

        if(this.analyzing is not null && !this.analyzing.IsCompleted)
        {
            await this.analyzing.ConfigureAwait(false);
        }

        if(this.disposeStreamManager)
        {
            await this.bufferedStreamManager.DisposeAsync().ConfigureAwait(false);
        }

        this.indices?.Dispose();

        this.isDisposed = true;
    }
}

