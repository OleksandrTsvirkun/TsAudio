using System;
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

    protected int index;
    protected bool disposed;

    public override long Position
    {
        get
        {
            if(this.Indices.Count == 0 || this.index == this.Indices.Count)
            {
                var last = this.Indices.LastOrDefault();
                return last.SamplePosition + last.SampleCount;
            }

            return this.Indices[this.index].SamplePosition;
        }
    }

    public abstract Mp3WaveFormat Mp3WaveFormat { get; }

    protected abstract IReadOnlyList<Mp3Index> Indices { get; }

    protected abstract IMp3FrameDecompressor Decompressor { get; }

    protected abstract IWaveBuffer WaveBuffer { get; }

    protected abstract Task DecodingTask { get; }

    protected abstract CancellationTokenSource DecodingCancellationTokenSource { get; }

    public Mp3WaveStream(Stream stream, int bufferSize = ushort.MaxValue, IMp3FrameFactory? frameFactory = null)
    {
        this.index = 0;
        this.stream = stream;
        this.bufferSize = bufferSize;
        this.frameFactory = frameFactory ?? Mp3FrameFactory.Instance;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return this.WaveBuffer.ReadAsync(buffer, cancellationToken);
    }

    public override async ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        using var locker = await this.repositionLock.LockAsync(cancellationToken).ConfigureAwait(false);
        using var decodingLock = this.waitForDecoding.Lock();

        var last = this.Indices.LastOrDefault();

        position = Math.Clamp(position, 0, last.SamplePosition + last.SampleCount);

        if(position == 0)
        {
            this.index = 0;
            return;
        }

        var midIndex = this.Indices.IndexOfNear(position, static x => x.SamplePosition);

        this.index = Math.Max(0, midIndex);
        await this.WaveBuffer.ResetAsync(cancellationToken).ConfigureAwait(false);
        this.Decompressor.Reset();
    }

    protected Task DecodeAsync()
    {
        return Task.Run(this.DecodeAsyncImpl, this.DecodingCancellationTokenSource.Token);
    }

    protected virtual ValueTask DisposeCoreAsync()
    {
        return ValueTask.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        if(this.disposed)
        {
            return;
        }
        
        if(!this.DecodingCancellationTokenSource.IsCancellationRequested)
        {
            this.DecodingCancellationTokenSource.Cancel();
            this.DecodingCancellationTokenSource.Dispose();
        }

        if (!this.DecodingTask.IsCompleted)
        {
            await this.DecodingTask.ConfigureAwait(false);
        }

        await this.DisposeCoreAsync().ConfigureAwait(false);

        this.repositionLock.Dispose();
        this.waitForDecoding.Dispose();
        this.Decompressor?.Dispose();
        await this.stream.DisposeAsync().ConfigureAwait(false);
        await this.WaveBuffer.DisposeAsync().ConfigureAwait (false);

        this.disposed = true;
    }

    protected virtual async Task DecodeAsyncImpl()
    {
        var cancellationToken = this.DecodingCancellationTokenSource.Token;

        if(this.Indices.Count == 0)
        {
            throw new ArgumentException("Not found any MP3 frame indices.", nameof(this.Indices.Count));
        }

        while(!cancellationToken.IsCancellationRequested && !this.disposed)
        {
            SemaphoreSlimHolder holder = default;
            try
            {
                holder = await this.repositionLock.LockAsync(cancellationToken);

                if(this.index >= this.Indices.Count)
                {
                    await this.DecodeExtraWaitAsync(cancellationToken);
                    continue;
                }

                var index = this.Indices[this.index++];

                var frame = await this.frameFactory.LoadFrameAsync(this.stream, index, cancellationToken);

                if(frame is null)
                {
                    continue;
                }

                using var samples = this.Decompressor.DecompressFrame(frame);

                if (samples is not null)
                {
                    await this.WaveBuffer.WriteAsync(samples.Memory, cancellationToken);
                }
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
            finally
            {
                if(!this.disposed)
                {
                    holder.Dispose();
                }
            }
        }
    }

    protected virtual async ValueTask DecodeExtraWaitAsync(CancellationToken cancellationToken = default)
    {
        await this.waitForDecoding.ResetAndGetAwaiterWithSoftCancellation(cancellationToken);
    }
}
