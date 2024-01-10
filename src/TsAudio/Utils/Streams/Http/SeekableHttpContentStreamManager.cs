using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils.Threading;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStreamManager : ISeekableStreamManager
{
    private readonly ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
    
    private readonly HttpClient httpClient;
    private readonly Func<HttpRequestMessage> RequestFactory;

    private readonly ManualResetEventSlim WriteAwaiter = new(true);
    private readonly ManualResetEventSlim ReadAwaiter = new(false);

    private bool isInitialized;
    private Stream stream;

    private BufferSegment<byte> WriteSegment;
    private BufferSegment<byte> ReadSegment;

    private int ReadOffset;
    private int WriteOffset;

    private Memory<byte> WriteBuffer => this.WriteSegment.MemoryOwner.Memory.Slice(this.WriteOffset); 

    private ReadOnlyMemory<byte> ReadBuffer => this.ReadSegment.MemoryOwner.Memory.Slice(this.ReadOffset);

    private long ReadPosition => this.ReadSegment.Position + this.ReadOffset;

    private long WritePosition => this.WriteSegment.Position + this.WriteOffset;

    public BufferingOptions BufferingOptions { get; }

    private long length;
    public long Length => this.length;

    public SeekableHttpContentStreamManager(HttpClient httpClient, Func<HttpRequestMessage> requestBuilder, BufferingOptions bufferingOptions)
    {
        this.httpClient = httpClient;
        this.RequestFactory = requestBuilder;
        this.BufferingOptions = bufferingOptions;

        this.ResetBuffer(0);
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        using var holder = this.locker.AquireWriteLock();

        if(this.isInitialized)
        {
            return;
        }

        var request = this.RequestFactory();

        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var contentLengthString = response.Content.Headers.NonValidated["Content-Length"].ToString();
        long.TryParse(contentLengthString, out var contentLength);
        this.length = response.Content.Headers.ContentLength ?? contentLength;
        this.stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await this.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        throw new System.NotImplementedException();
    }

    public ValueTask<IStreamReader> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        IStreamReader stream = mode switch
        {
            StreamReadMode.Kick => new SeekableHttpContentStreamKickReader(new()
            {
                SeekAsync = this.SeekAsync,
                ConsumeAsync = this.ConsumeAsync,
                GetBuffer = () => this.ReadBuffer,
                Length = this.Length,
                Locker = this.locker
            }),
            StreamReadMode.Wait => new SeekableHttpContentStreamWaitReader(new()
            {
                ReadAwaiter = this.ReadAwaiter,
                GetBuffers = this.GetBuffers,
                Length = this.Length,
                Locker = this.locker
            }),
            _ => throw new NotImplementedException()
        };

        return ValueTask.FromResult(stream);
    }

    private IEnumerable<BufferMemorySegment<byte>> GetBuffers()
    {
        var readSegment = this.ReadSegment;
        var writeSegment = this.WriteSegment;
        var readOffset = this.ReadOffset;
        var writeOffset = this.WriteOffset;

        if (readSegment != writeSegment)
        {
            yield return new BufferMemorySegment<byte>() 
            { 
                Memory = readSegment.MemoryOwner.Memory.Slice(readOffset),
                Position = readSegment.Position + readOffset
            };
            readOffset = 0;
        }

        while(readSegment != writeSegment)
        {
            yield return new BufferMemorySegment<byte>()
            {
                Memory = readSegment.MemoryOwner.Memory,
                Position = readSegment.Position
            };
        }

        if (readSegment == writeSegment)
        {
            yield return new BufferMemorySegment<byte>()
            {
                Memory = readSegment.MemoryOwner.Memory.Slice(readOffset, writeOffset - readOffset),
                Position = readSegment.Position
            };
        }
    }

    private async ValueTask SeekAsync(long position, CancellationToken cancellationToken)
    {
        using var holder = this.locker.AquireWriteLock();

        if (this.ReadSegment.Position <= position && position < this.WritePosition)
        {
            var offset = position - this.ReadSegment.Position;

            while(offset > this.ReadSegment.MemoryOwner.Memory.Length)
            {
                offset %= this.ReadSegment.MemoryOwner.Memory.Length;
                var oldSegment = this.ReadSegment;
                this.ReadSegment = this.ReadSegment.Next;
                oldSegment.Dispose();
            }

            if(this.ShouldResumeWriting())
            {
                await this.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
                this.WriteAwaiter.Set();
            }

            return;
        }

        await this.ReadloadStreamAsync(position, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ConsumeAsync(long position, CancellationToken cancellationToken)
    {
        using var holder = this.locker.AquireWriteLock();

        if(this.ReadSegment.Position <= position && position < this.WritePosition)
        {
            var offset = position - this.ReadSegment.Position;

            while(offset > this.ReadSegment.MemoryOwner.Memory.Length)
            {
                offset %= this.ReadSegment.MemoryOwner.Memory.Length;
                var oldSegment = this.ReadSegment;
                this.ReadSegment = this.ReadSegment.Next;
                oldSegment.Dispose();
            }
        }

        if(this.ShouldResumeWriting())
        {
            await this.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Stream> GetHttpContentStreamAsync(long position = 0, CancellationToken cancellationToken = default)
    {
        var request = this.RequestFactory();
        request.Headers.Range = new RangeHeaderValue(position, this.length);

        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask LoadIntoBufferAsync(CancellationToken cancellationToken)
    {
        var totalRead = 0;
        
        while(!this.ShouldPauseWriting())
        {
            var read = await this.stream.ReadAsync(this.WriteBuffer, cancellationToken).ConfigureAwait(false);

            if(read == 0)
            {
                break;
            }

            this.WriteOffset += read;
            totalRead += read;
            if (this.WriteOffset >= this.WriteSegment.MemoryOwner.Memory.Length)
            {
                this.WriteOffset %= this.WriteSegment.MemoryOwner.Memory.Length;
                this.WriteSegment.Next ??= this.CreateNextSegment(this.WriteSegment);
                this.WriteSegment = this.WriteSegment.Next;
            }  
        }

        if (totalRead > 0)
        {
            this.ReadAwaiter.Set();
        }

        if(this.ShouldPauseWriting())
        {
            this.WriteAwaiter.Reset();
        }
    }

    private async ValueTask ReadloadStreamAsync(long position, CancellationToken cancellationToken = default)
    {
        var oldStream = this.stream;

        this.stream = await this.GetHttpContentStreamAsync(position, cancellationToken).ConfigureAwait(false);

        this.ResetBuffer(position);

        await this.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);

        if(oldStream is not null)
        {
            await oldStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private BufferSegment<byte> CreateNextSegment(BufferSegment<byte> segment)
    {
        var position = segment.Position + segment.MemoryOwner.Memory.Length;
        var memoryOwner = MemoryPool<byte>.Shared.Rent(this.BufferingOptions.BufferSize);
        return new BufferSegment<byte>(memoryOwner, position);
    }

    private void ResetBuffer(long position)
    {
        var readSegment = this.ReadSegment;

        var memoryOwner = MemoryPool<byte>.Shared.Rent(this.BufferingOptions.BufferSize);
        var segment = new BufferSegment<byte>(memoryOwner, position);
        
        this.WriteOffset = 0;
        this.ReadOffset = 0;
        this.WriteSegment = segment;
        this.ReadSegment = segment;

        do
        {
            readSegment.Dispose();
            readSegment = readSegment.Next;
        } while(readSegment is not null);
    }

    private bool ShouldPauseWriting()
    {
        return this.WritePosition - this.ReadPosition > this.BufferingOptions.PauseWriterThreshold;
    }

    private bool ShouldResumeWriting()
    {
        return this.WritePosition - this.ReadPosition < this.BufferingOptions.ResumeWriterThreshold;
    }
}
