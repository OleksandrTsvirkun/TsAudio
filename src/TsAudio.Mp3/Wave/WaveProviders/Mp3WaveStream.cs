using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using TsAudio.Formats.Mp3;

using TsAudio.Wave.WaveFormats;

using TsAudio.Wave.WaveStreams;
using System.IO;

namespace TsAudio.Wave.WaveProviders;

public class Mp3WaveStream : WaveStream
{
    private readonly BufferedWaveProvider waveProvider;
    private readonly Task parsing;
    private readonly object repositionLock = new();
    private readonly IReadOnlyList<Mp3Index> indices;
    private readonly IMp3FrameFactory frameFactory;
    private readonly Stream reader;
    private readonly ManualResetEventSlim waitForDecoding = new(false);
    private readonly ManualResetEventSlim waitForParse;
    private readonly IMp3FrameDecompressor decompressor;
    private readonly Task decoding;
    private readonly CancellationTokenSource decodeCancellationTokenSource;

    private int index;

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

    internal Mp3WaveStream(Mp3WaveStreamArgs args)
    {
        this.reader = args.Reader;
        this.frameFactory = args.FrameFactory;
        this.indices = args.Indices;
        this.Mp3WaveFormat = args.Mp3WaveFormat;
        this.Length = args.TotalSamples;
        this.decompressor = new Mp3FrameDecompressor(this.Mp3WaveFormat);
        this.WaveFormat = this.decompressor.WaveFormat;
        this.waveProvider = new BufferedWaveProvider(this.WaveFormat, ushort.MaxValue*4);
        this.parsing = args.Parsing;
        this.waitForParse = args.ParseWait;
        this.decodeCancellationTokenSource = new();
        this.decoding = this.DecodeAsync(this.decodeCancellationTokenSource.Token);
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
        return Task.Run(async () =>
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if(this.index >= this.indices.Count)
                    {
                        if(this.parsing.IsCompleted)
                        {
                            await this.waveProvider.FlushAsync(cancellationToken);
                            this.waitForDecoding.Reset();
                            this.waitForDecoding.Wait(cancellationToken);
                        }
                        else
                        {
                            this.waitForParse.Reset();
                            this.waitForParse.Wait(cancellationToken);
                        }

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
        }, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            this.decodeCancellationTokenSource.Cancel();
        }

        base.Dispose(disposing);
    }
}