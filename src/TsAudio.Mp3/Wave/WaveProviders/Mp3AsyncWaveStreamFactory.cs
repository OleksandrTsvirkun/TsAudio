using Collections.Pooled;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils.Streams;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveProviders;

public class Mp3AsyncWaveStreamFactory : IAsyncWaveStreamFactory
{
    public static async ValueTask<Mp3AsyncWaveStreamFactory> FromStreamAsync(Stream stream, WaveStreamMetadata metadata, CancellationToken cancellationToken = default)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (stream is FileStream fileStream)
        {
            metadata.StreamLength = fileStream.Length;
        }

        CacheStream cacheStream = stream as CacheStream;

        if(cacheStream is null)
        {
            cacheStream = new CacheStream(metadata.StreamLength);

            var loading = Task.Run(async () =>
            {
                var buffer = ArrayPool<byte>.Shared.Rent(4096 * 32);

                try
                {
                    while(true)
                    {
                        var read = await stream.ReadAsync(buffer, cancellationToken);

                        await cacheStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }

                    await cacheStream.FlushAsync(cancellationToken);
                }
                catch(Exception)
                {

                }
            });
        }

        var instance = new Mp3AsyncWaveStreamFactory();
        instance.cacheStream = cacheStream;
        var mode = metadata.TotalSamples is 0 ? ReaderMode.Kick : ReaderMode.Wait;

        instance.reader = cacheStream.GetReader(mode);

        await instance.InitAsync(cancellationToken);

        if(metadata.TotalSamples is 0)
        {
            await instance.Loading;

            metadata.TotalSamples = instance.SampleCount;
            
        }

        instance.Metadata = metadata;

        return instance;
    }

    protected CacheStream cacheStream;
    protected CacheStream.Reader reader;
    protected readonly IMp3FrameFactory frameFactory;
    protected readonly PooledList<Mp3Index> indices = new();
    protected readonly ConcurrentBag<Mp3WaveStream> waveStreams = new();

    private CancellationTokenSource cts;

    private IAsyncEnumerator<(Mp3Index Index, Mp3Frame Frame)> framesEnumerator;

    public Task Loading { get; private set; }

    public Mp3WaveFormat Mp3WaveFormat { get; private set; }

    public long SampleCount
    {
        get
        {
            var last = this.indices.Count > 0 ? this.indices[this.indices.Count - 1] : default;
            return last.SampleCount + last.SamplePosition;
        }
    }

    public WaveStreamMetadata Metadata { get; private set; }

    private Mp3AsyncWaveStreamFactory()
    {
        this.frameFactory = Mp3FrameFactory.Instance;
    }

    public Mp3AsyncWaveStreamFactory(CacheStream stream, WaveStreamMetadata metadata) : this()
    {
        this.cacheStream = stream;
        this.reader = stream.GetReader();
        this.Metadata = metadata;
    }

    public ValueTask<IWaveStream> GetWaveProviderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = this.cacheStream.GetReader(mode);
        var waveProvider = new Mp3WaveStream(reader, this.frameFactory, this.Mp3WaveFormat, this.indices, this.Metadata.TotalSamples, this.Loading, cancellationToken);
        this.waveStreams.Add(waveProvider);
        return new ValueTask<IWaveStream>(waveProvider);
    }

    public async ValueTask InitAsync(CancellationToken cancellationTokenExternal = default)
    {
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);

        var cancellationToken = this.cts.Token;

        this.framesEnumerator = this.frameFactory.LoadFrameHeadersAsync(this.reader, cancellationToken).GetAsyncEnumerator(cancellationToken);

        var hasFrames = await this.framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        var frameDescriptor = this.framesEnumerator.Current;

        this.indices.Add(frameDescriptor.Index);

        var frame = frameDescriptor.Frame;

        this.Mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate,
                                                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                frame.FrameLength,
                                                frame.BitRate);

        this.Loading = LoadAsync(cancellationToken);
    }

    private Task LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.Factory.StartNew(async () =>
        {
            try
            {
                await foreach(var (index, frame) in this.frameFactory.LoadFrameHeadersAsync(this.reader, cancellationToken))
                {
                    this.indices.Add(index);

                    foreach(var reader in this.waveStreams)
                    {
                        reader.KickWaiter();
                    }
                }

                foreach(var reader in this.waveStreams)
                {
                    reader.KickWaiter();
                }
            }
            catch(OperationCanceledException)
            {

            }
        }, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default).Unwrap();
    }

    public void Dispose()
    {
        if (!this.cts?.IsCancellationRequested ?? false)
        {
            this.cts.Cancel();
        }

        this.cts?.Dispose();

        foreach(var waveStream in this.waveStreams)
        {
            waveStream.Dispose();
        }

        this.indices.Dispose();
    }
}

