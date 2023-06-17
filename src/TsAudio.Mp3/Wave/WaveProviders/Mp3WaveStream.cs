﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using TsAudio.Formats.Mp3;

using TsAudio.Wave.WaveFormats;

using TsAudio.Wave.WaveStreams;
using System.IO;
using TsAudio.Utils;
using TsAudio.Utils.Threading;

namespace TsAudio.Wave.WaveProviders;

public class Mp3WaveStream : WaveStream
{
    private readonly BufferedWaveProvider waveProvider;
    private readonly Task parsing;
    private readonly SemaphoreSlim repositionLock = new(1, 1);
    private readonly IReadOnlyList<Mp3Index> indices;
    private readonly IMp3FrameFactory frameFactory;
    private readonly Stream reader;
    private readonly ManualResetEventSlim waitForDecoding = new(true);
    private readonly ManualResetEventSlim waitForParse;
    private readonly IMp3FrameDecompressor decompressor;
    private readonly Task decoding;
    private readonly CancellationTokenSource decodeCancellationTokenSource;

    private int index;

    public override long? TotalSamples { get; }

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
    }

    public override WaveFormat WaveFormat { get; }

    public Mp3WaveFormat Mp3WaveFormat { get; }

    internal Mp3WaveStream(Mp3WaveStreamArgs args)
    {
        this.reader = args.Reader;
        this.frameFactory = args.FrameFactory;
        this.indices = args.Indices;
        this.Mp3WaveFormat = args.Mp3WaveFormat;
        this.TotalSamples = args.TotalSamples;
        this.decompressor = new Mp3FrameDecompressor(this.Mp3WaveFormat);
        this.WaveFormat = this.decompressor.WaveFormat;
        this.waveProvider = new BufferedWaveProvider(this.WaveFormat, ushort.MaxValue*4);
        this.parsing = args.Analyzing;
        this.waitForParse = args.ParseWait;
        this.decodeCancellationTokenSource = new();
        this.decoding = this.DecodeAsync(this.decodeCancellationTokenSource.Token);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.waveProvider.ReadAsync(buffer, cancellationToken);
    }

    private Task DecodeAsync(CancellationToken cancellationToken = default)
    {
        return Task.Factory.StartNew(async () =>
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

                    if(frame is null)
                    {
                        continue;
                    }

                    using var samples = this.decompressor.DecompressFrame(frame);

                    await this.waveProvider.WriteAsync(samples.Memory, cancellationToken);
                }
                catch(OperationCanceledException)
                {
                    return;
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
        }, cancellationToken, TaskCreationOptions.AttachedToParent | TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    protected override void Dispose(bool disposing)
    {
        this.decodeCancellationTokenSource.Cancel();
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken);

        this.waitForDecoding.Reset();

        var last = this.indices.LastOrDefault();

        position = Math.Clamp(position, 0, last.SamplePosition + last.SampleCount);

        if(position == 0)
        {
            this.index = 0;
            return;
        }

        var midIndex = this.indices.IndexOfNear(position, static x => x.SamplePosition);

        this.index = Math.Max(0, midIndex - 2);
        await this.waveProvider.ResetAsync();
        this.decompressor.Reset();
        this.waitForDecoding.Set();
    }

    public override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        return default;
    }
}