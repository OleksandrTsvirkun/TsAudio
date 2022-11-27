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
    public static async ValueTask<Mp3AsyncWaveStreamFactory> FromStreamAsync(Stream stream, long length, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        CacheStream cacheStream = stream as CacheStream;

        if(cacheStream is null)
        {
            cacheStream = new CacheStream(length);

            var loading = Task.Run(async () =>
            {
                var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

                try
                {
                    while(true)
                    {
                        var read = await stream.ReadAsync(buffer, cancellationToken);

                        await cacheStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }
                catch(Exception)
                {

                }
                finally
                {
                    await cacheStream.FlushAsync(cancellationToken);
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            });
        }

        var instance = new Mp3AsyncWaveStreamFactory();
        instance.cacheStream = cacheStream;

        await instance.InitAsync(cancellationToken);

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

    public long Length { get; private set; }

    private Mp3AsyncWaveStreamFactory()
    {
        this.frameFactory = Mp3FrameFactory.Instance;
    }

    public Mp3AsyncWaveStreamFactory(CacheStream stream) : this()
    {
        this.cacheStream = stream;
        this.reader = stream.GetReader();
        this.Length = stream.Length;
    }

    public ValueTask<IWaveStream> GetWaveProviderAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var reader = this.cacheStream.GetReader(mode);
        var waveProvider = new Mp3WaveStream(reader, this.frameFactory, this.Mp3WaveFormat, this.indices, this.Length, this.Loading, cancellationToken);
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

        var firstFrameDescriptor = this.framesEnumerator.Current;

        hasFrames = await this.framesEnumerator.MoveNextAsync(cancellationToken);

        if(!hasFrames)
        {
            throw new InvalidOperationException("Stream does not contain Mp3Frames");
        }

        var secondFrameDescriptor = this.framesEnumerator.Current;

        var firstFrame = firstFrameDescriptor.Frame;
        var secondFrame = secondFrameDescriptor.Frame;

        if (firstFrame.SampleRate != secondFrame.SampleRate
            || firstFrame.ChannelMode != secondFrame.ChannelMode)
        {
            firstFrameDescriptor = secondFrameDescriptor;
        }

        if (!firstFrameDescriptor.Equals(secondFrameDescriptor))
        {
            this.indices.Add(firstFrameDescriptor.Index);
        }

        this.indices.Add(secondFrameDescriptor.Index);

        var frame = firstFrameDescriptor.Frame;

        this.Mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate,
                                                frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                                                frame.FrameLength,
                                                frame.BitRate);

        this.Loading = LoadAsync(cancellationToken);
    }

    private async Task<Task> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Factory.StartNew(async () =>
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
        }, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously | TaskCreationOptions.AttachedToParent, TaskScheduler.Default);
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

