using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Formats.Mp3;
using TsAudio.Utils;
using TsAudio.Utils.Threading;
using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

namespace TsAudio.Wave.WaveProviders;

public abstract class Mp3WaveStream : WaveStream
{
    protected readonly Stream stream;
    protected readonly IMp3FrameFactory frameFactory;
    
    protected readonly ManualResetEventSlim waitForDecoding = new(true);
    protected readonly SemaphoreSlim repositionLock = new(1, 1);

    protected readonly int bufferSize; 

    protected IWaveBuffer waveProvider;
    protected IMp3FrameDecompressor decompressor;
    protected IReadOnlyList<Mp3Index> indices;
    protected CancellationTokenSource decodeCts;
    protected Task decoding;
    protected int index;
    protected bool disposed;
    protected Mp3WaveFormat mp3WaveFormat;
    protected WaveFormat waveFormat;

    public override WaveFormat WaveFormat => this.waveFormat;

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

    public virtual Mp3WaveFormat Mp3WaveFormat => this.mp3WaveFormat;

    public Mp3WaveStream(Stream stream, int bufferSize = ushort.MaxValue, IMp3FrameFactory? frameFactory = null)
    {
        this.stream = stream;
        this.bufferSize = bufferSize;
        this.frameFactory = frameFactory ?? Mp3FrameFactory.Instance;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.waveProvider.ReadAsync(buffer, cancellationToken);
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken);
        using var decodingLock = this.waitForDecoding.Lock();

        var last = this.indices.LastOrDefault();

        position = Math.Clamp(position, 0, last.SamplePosition + last.SampleCount);

        if(position == 0)
        {
            this.index = 0;
            return;
        }

        var midIndex = this.indices.IndexOfNear(position, static x => x.SamplePosition);

        this.index = Math.Max(0, midIndex - 2);
        await this.waveProvider.ResetAsync(cancellationToken);
        this.decompressor.Reset();
    }

    protected Task DecodeAsync()
    {
        return Task.Run(this.DecodeAsyncImpl, this.decodeCts.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if(disposing)
        {
            this.repositionLock.Dispose();
            this.waitForDecoding.Dispose();
            this.decompressor?.Dispose();
        }

        base.Dispose(disposing);
        this.disposed = true;
    }

    protected virtual async Task DecodeAsyncImpl()
    {
        var cancellationToken = this.decodeCts.Token;

        if(this.indices.Count == 0)
        {
            throw new ArgumentException("Not found any MP3 frame indices.", nameof(this.indices.Count));
        }

        while(!cancellationToken.IsCancellationRequested && !this.disposed)
        {
            try
            {
                if(this.index >= this.indices.Count)
                {
                    await this.DecodeExtraWaitAsync(cancellationToken);
                    continue;
                }

                var index = this.indices[this.index++];

                var frame = await this.frameFactory.LoadFrameAsync(this.stream, index, cancellationToken);

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
    }

    protected virtual async ValueTask DecodeExtraWaitAsync(CancellationToken cancellationToken = default)
    {
        await this.waitForDecoding.ResetAndGetAwaiterWithCancellation(cancellationToken);
    }
}
