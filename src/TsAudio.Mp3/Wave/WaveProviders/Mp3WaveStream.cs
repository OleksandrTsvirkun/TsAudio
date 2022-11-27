using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using TsAudio.Formats.Mp3;

using TsAudio.Wave.WaveFormats;

using TsAudio.Wave.WaveStreams;
using System.IO;
using Collections.Pooled;
using System.Runtime.Serialization;

namespace TsAudio.Wave.WaveProviders;

public class Mp3WaveStream : WaveStream
{
    private readonly BufferedWaveProvider waveProvider;
    private readonly Task loading;
    private readonly object repositionLock = new();
    private readonly IReadOnlyList<Mp3Index> indices;
    private readonly IMp3FrameFactory frameFactory;
    private readonly Stream reader;
    private readonly ManualResetEvent waitForDecoding = new(false);
    private readonly IMp3FrameDecompressor decompressor;
    private int index;
    private Task decoding;

    public override bool CanSeek => this.reader.CanSeek;

    public override long Position
    {
        get
        {
            if(this.indices.Count == 0 || this.index == this.indices.Count)
            {
                var last = this.indices.LastOrDefault();
                return last.SamplePosition + last.SampleCount;
            }

            return this.indices[this.index].SamplePosition;
        }
        set
        {
            try
            {
                this.SetPosition(value);
            }
            catch(Exception ex)
            {

            }
        }
    }

    public override long Length { get; }

    public override WaveFormat WaveFormat { get; }

    public Mp3WaveFormat Mp3WaveFormat { get; }

    internal Mp3WaveStream(Stream reader, IMp3FrameFactory frameFactory, Mp3WaveFormat mp3WaveFormat, IReadOnlyList<Mp3Index> indices, long length, Task loading, CancellationToken cancellationToken = default)
    {
        this.reader = reader;
        this.frameFactory = frameFactory;
        this.indices = indices;
        this.Mp3WaveFormat = mp3WaveFormat;
        this.Length = length;
        this.decompressor = new Mp3FrameDecompressor(this.Mp3WaveFormat);
        this.WaveFormat = this.decompressor.WaveFormat;
        this.waveProvider = new BufferedWaveProvider(this.WaveFormat, ushort.MaxValue*4);
        this.loading = loading;
        this.decoding = this.DecodeAsync(cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.waveProvider.ReadAsync(buffer, cancellationToken);
    }

    private void SetPosition(long value)
    {
        this.waitForDecoding.Reset();

        lock(repositionLock)
        {
            var last = this.indices.LastOrDefault();

            value = Math.Max(Math.Min(value, last.SamplePosition + last.SampleCount), 0);

            if(value == 0)
            {
                this.index = 0;
                return;
            }

            var minIndex = 0;
            var maxIndex = this.indices.Count - 1;
            var midIndex = (minIndex + maxIndex) / 2;

            while(minIndex <= maxIndex)
            {
                midIndex = (minIndex + maxIndex) / 2;

                if(value < indices[midIndex].SamplePosition)
                {
                    maxIndex = midIndex - 1;
                }
                else
                {
                    minIndex = midIndex + 1;
                }
            }

            this.index = Math.Max(0, midIndex - 2);
            this.waveProvider.Clear();
            this.decompressor.Reset();
            this.waitForDecoding.Set();
        }
    }

    private Task DecodeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Factory.StartNew(async () =>
        {
            using var registration = cancellationToken.Register(() =>
            {
                this.waitForDecoding.Set();
            });

            while(true)
            {
                try
                {
                    bool isLast = false;

                    lock(repositionLock)
                    {
                        isLast = this.index == this.indices.Count;
                    }

                    if(isLast)
                    {
                        if(this.loading.IsCompleted)
                        {
                            await this.waveProvider.FlushAsync(cancellationToken);
                        }

                        this.waitForDecoding.Reset();
                        this.waitForDecoding.WaitOne();
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }


                    var index = this.indices[this.index++];


                    var frame = await this.frameFactory.LoadFrameAsync(this.reader, index, cancellationToken);

                    if(!frame.HasValue)
                    {
                        continue;
                    }

                    using var samples = this.decompressor.DecompressFrame(frame.Value);

                    await this.waveProvider.WriteAsync(samples, cancellationToken);
                }
                catch(OperationCanceledException)
                {
                    break;
                }
                catch(InvalidOperationException ex)
                {
                    continue;
                }
                catch(InvalidDataException ex)
                {
                    continue;
                }
                catch(ArgumentOutOfRangeException ex)
                {
                    continue;
                }
                catch(Exception ex)
                {
                    return;
                }

            }
        }, cancellationToken, TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default).Unwrap();

    }

    internal void KickWaiter()
    {
        this.waitForDecoding.Set();
    }
}