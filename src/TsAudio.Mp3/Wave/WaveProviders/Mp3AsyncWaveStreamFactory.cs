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

public class Mp3AsyncWaveStreamFactoryArgs 
{
    public BufferedStreamManager StreamManager { get; set; }

    public long? TotalSamples { get; set; }
}

internal class Mp3WaveStreamArgs
{
    public IMp3FrameFactory FrameFactory { get; init; }

    public IReadOnlyList<Mp3Index> Indices { get; init; }

    public long TotalSamples { get; init; }

    public Task Parsing { get; init; }

    public Mp3WaveFormat Mp3WaveFormat { get; init; }

    public Stream Reader { get; init; }

    public ManualResetEventSlim ParseWait { get; init; }
}


public class Mp3AsyncWaveStreamFactory : IAsyncWaveStreamFactory
{
    protected readonly BufferedStreamManager bufferedStreamManager;

    protected readonly IMp3FrameFactory frameFactory;
    protected readonly PooledList<Mp3Index> indices;
    protected readonly ConcurrentBag<Mp3WaveStream> waveStreams;
    protected readonly ManualResetEventSlim consumeWaiter;
    protected Stream? reader;
    protected CancellationTokenSource? cts;
    protected IAsyncEnumerator<(Mp3Index Index, Mp3Frame Frame)>? framesEnumerator;

    public Task Parsing { get; private set; }

    public Mp3WaveFormat Mp3WaveFormat { get; private set; }

    public long SampleCount
    {
        get
        {
            var last = this.indices.Count > 0 ? this.indices[this.indices.Count - 1] : default;
            return last.SampleCount + last.SamplePosition;
        }
    }


    public long? TotalSamples { get; private set; }

    public Mp3AsyncWaveStreamFactory(Mp3AsyncWaveStreamFactoryArgs args)
    {
        this.frameFactory = Mp3FrameFactory.Instance;
        this.bufferedStreamManager = args.StreamManager;
        this.TotalSamples = args.TotalSamples;
        this.indices = new();
        this.waveStreams = new();
        this.consumeWaiter = new();
        this.reader = null;
        this.cts = null;
        this.framesEnumerator = null;
    }

    public async Task InitAsync(CancellationToken cancellationTokenExternal = default)
    {
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);

        var cancellationToken = this.cts.Token;

        this.reader = await this.bufferedStreamManager.GetBufferedStreamManagerReaderAsync(cancellationToken: cancellationToken);

        this.framesEnumerator = this.frameFactory.LoadFrameHeadersAsync(this.reader, cancellationToken).GetAsyncEnumerator(cancellationToken);

        var hasFrames = await this.framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        var firstFrameDescriptor = this.framesEnumerator.Current;

        hasFrames = await this.framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        var secondFrameDescriptor = this.framesEnumerator.Current;

        var firstFrame = firstFrameDescriptor.Frame;
        var secondFrame = secondFrameDescriptor.Frame;

        if(firstFrame.SampleRate != secondFrame.SampleRate
            || firstFrame.ChannelMode != secondFrame.ChannelMode)
        {
            firstFrameDescriptor = secondFrameDescriptor;
        }

        if(!firstFrameDescriptor.Equals(secondFrameDescriptor))
        {
            this.indices.Add(firstFrameDescriptor.Index);
        }

        this.indices.Add(secondFrameDescriptor.Index);

        var frame = firstFrameDescriptor.Frame;

        this.Mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate,
                                                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                frame.FrameLength,
                                                frame.BitRate);

        this.Parsing = this.ParseAsync(cancellationToken);
    }

    public async ValueTask<IWaveStream> GetWaveProviderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = await this.bufferedStreamManager.GetBufferedStreamManagerReaderAsync(mode, cancellationToken);

        var args = new Mp3WaveStreamArgs()
        {
            ParseWait = this.consumeWaiter,
            FrameFactory = this.frameFactory,
            Indices = this.indices,
            TotalSamples = this.TotalSamples ?? this.SampleCount,
            Mp3WaveFormat = this.Mp3WaveFormat,
            Parsing = this.Parsing,
            Reader = reader,
        };
        var waveProvider = new Mp3WaveStream(args);

        this.waveStreams.Add(waveProvider);
        return waveProvider;
    }

    private Task ParseAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                await foreach(var (index, frame) in this.frameFactory.LoadFrameHeadersAsync(this.reader, cancellationToken))
                {
                    this.indices.Add(index);
                    this.consumeWaiter.Set();
                }

                foreach(var reader in this.waveStreams)
                {
                    this.consumeWaiter.Set();
                }
            }
            catch(OperationCanceledException)
            {

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

        foreach(var waveStream in this.waveStreams)
        {
            waveStream.Dispose();
        }

        this.indices.Dispose();
    }
}

