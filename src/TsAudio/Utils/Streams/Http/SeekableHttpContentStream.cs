using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.Http;

public class SeekableHttpContentStream : Stream
{
    private readonly SemaphoreSlim locker = new SemaphoreSlim(1, 1);
    private readonly HttpClient httpClient;
    private readonly Func<HttpRequestMessage> requestBuilder;
    private readonly byte[] buffer;
    private bool isBufferEmpty;
    private int readPosition;
    private int writePosition;
    private bool isInitialized;

    private Stream stream;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    private long length;
    public override long Length => this.length;

    private long position;
    public override long Position
    {
        get => this.position;
        set => this.Seek(value, SeekOrigin.Begin);
    }

    public SeekableHttpContentStream(HttpClient httpClient, Func<HttpRequestMessage> requestBuilder, int bufferSize, bool initSynchronusly = false)
    {
        this.httpClient = httpClient;
        this.requestBuilder = requestBuilder;
        this.buffer = new byte[bufferSize];
        this.isBufferEmpty = true;

        if(initSynchronusly)
        {
            this.Init();
        }
    }

    public ReadOnlyMemory<byte> GetBufferedData()
    {
        return this.GetInnerReadBuffer();
    }

    public async Task InitAsync(CancellationToken cancellationToken = default)
    {
        if(this.isInitialized)
        {
            return;
        }

        var request = this.requestBuilder();

        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.NonValidated["Content-Length"].ToString();
        this.length = response.Content.Headers.ContentLength ?? long.Parse(contentLength);
        this.stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Init()
    {
        if(this.isInitialized)
        {
            return;
        }

        using var request = this.requestBuilder();

        var response = this.httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.NonValidated["Content-Length"].ToString();
        this.length = response.Content.Headers.ContentLength ?? long.Parse(contentLength);
        this.stream = response.Content.ReadAsStream();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        var memory = buffer.AsMemory(offset, count);
        return this.ReadAsync(memory, cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await this.locker.WaitAsync(cancellationToken).ConfigureAwait(false);

        if(!this.isInitialized)
        {
            await this.InitAsync(cancellationToken).ConfigureAwait(false);
        }

        int totalRead = 0;

        this.TryReadFromBuffer(ref buffer, ref totalRead);

        if(buffer.Length == 0)
        {
            return totalRead;
        }

        while(buffer.Length > 0)
        {
            var innerBuffer = this.GetInnerWriteBuffer();

            var read = await this.stream!.ReadAsync(innerBuffer, cancellationToken).ConfigureAwait(false);

            this.writePosition += read;

            this.TryReadFromBuffer(ref buffer, ref totalRead);
        }

        this.position += totalRead;

        this.locker.Release();

        return totalRead;
    }

    private Memory<byte> GetInnerWriteBuffer()
    {
        return this.writePosition > this.readPosition
                                            ? this.buffer.AsMemory(this.writePosition)
                                            : this.writePosition < this.readPosition
                                                ? this.buffer.AsMemory(this.writePosition, this.readPosition - this.writePosition)
                                                : this.isBufferEmpty
                                                    ? this.buffer.AsMemory()
                                                    : Memory<byte>.Empty;
    }

    private void TryReadFromBuffer(ref Memory<byte> buffer, ref int totalRead)
    {
        var innerReadBuffer = this.GetInnerReadBuffer();

        while(innerReadBuffer.Length > 0 && buffer.Length > 0)
        {
            var toCopy = Math.Min(innerReadBuffer.Length, buffer.Length);

            innerReadBuffer.Slice(0, toCopy).CopyTo(buffer);

            buffer = buffer.Slice(toCopy);

            this.readPosition += toCopy;
            this.readPosition %= this.buffer.Length;

            totalRead += toCopy;
            innerReadBuffer = this.GetInnerReadBuffer();
        }

        this.isBufferEmpty = this.readPosition == this.writePosition;

        if(this.isBufferEmpty)
        {
            this.readPosition = 0;
            this.writePosition = 0;
        }
    }

    private Memory<byte> GetInnerReadBuffer()
    {
        return this.writePosition > this.readPosition
                                        ? this.buffer.AsMemory(this.readPosition, this.writePosition - this.readPosition)
                                        : this.writePosition < this.readPosition
                                            ? this.buffer.AsMemory(this.readPosition)
                                            : this.isBufferEmpty
                                                ? Memory<byte>.Empty
                                                : this.buffer.AsMemory(this.readPosition);
    }

    public override int ReadByte()
    {
        Span<byte> buffer = stackalloc byte[1];
        var read = this.Read(buffer);
        if(read == 0)
        {
            return -1;
        }

        return buffer[0];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var span = buffer.AsSpan(offset, count);
        return this.Read(span);
    }

    public override int Read(Span<byte> buffer)
    {
        this.locker.Wait();

        if(!this.isInitialized)
        {
            this.Init();
        }

        var read = this.stream!.Read(buffer);

        this.position += read;

        this.locker.Release();

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        this.locker.Wait();

        var position = origin switch
        {
            SeekOrigin.Begin => Math.Clamp(offset, 0, this.Length),
            SeekOrigin.Current => Math.Clamp(this.position + offset, 0, this.Length),
            SeekOrigin.End => Math.Clamp(this.Length - offset, 0, this.Length),
            _ => throw new NotSupportedException()
        };


        if(position > this.position)
        {
            var canSkip = this.buffer.Length - this.GetInnerWriteBuffer().Length;

            var needSkip = position - this.position;

            if(needSkip < int.MaxValue && (int)needSkip <= canSkip)
            {
                this.readPosition += (int)needSkip;
                this.readPosition %= this.buffer.Length;
            }
            else
            {
                this.ReadloadStream(position);
            }
        }
        else
        {
            this.ReadloadStream(position);
        }

        this.position = position;

        this.locker.Release();

        return this.position;
    }

    private void ReadloadStream(long position)
    {
        var oldStream = this.stream;
        this.stream = this.GetStream(position);

        oldStream?.Dispose();
    }

    private async ValueTask ReadloadStreamAsync(long position, CancellationToken cancellationToken = default)
    {
        var oldStream = this.stream;
        this.stream = await this.GetStreamAsync(position, cancellationToken).ConfigureAwait(false);

        if(oldStream is not null)
        {
            await oldStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }


    private async Task<Stream> GetStreamAsync(long position = 0, CancellationToken cancellationToken = default)
    {
        var request = this.requestBuilder();
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(position, this.length);

        var response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private Stream GetStream(long position = 0)
    {
        var request = this.requestBuilder();
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(position, this.length);

        var response = this.httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        return response.Content.ReadAsStream();
    }
}