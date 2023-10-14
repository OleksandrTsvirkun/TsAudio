using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders;

public class PipeWaveBuffer : IWaveBuffer
{
    private readonly Pipe pipe;

    public WaveFormat WaveFormat { get; }

    public PipeWaveBuffer(WaveFormat waveFormat, PipeOptions pipeOptions = null)
    {
        this.WaveFormat = waveFormat;
        this.pipe = pipeOptions is null ? new Pipe() : new Pipe(pipeOptions);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await this.pipe.Writer.FlushAsync(cancellationToken);
        await this.pipe.Writer.CompleteAsync();
    }

    public async ValueTask<int> ReadAsync(Memory<byte> memory, CancellationToken cancellationToken = default)
    {
        var reader = this.pipe.Reader;
        var read = 0;

        while(memory.Length > 0)
        {
            var result = await reader.ReadAtLeastAsync(memory.Length, cancellationToken);
            var buffer = result.Buffer;

            var toCopy = (int)Math.Min(memory.Length, buffer.Length);
            buffer.Slice(0, toCopy).CopyTo(memory.Span);

            buffer = buffer.Slice(toCopy);
            memory = memory.Slice(toCopy);

            reader.AdvanceTo(buffer.Start, buffer.End);

            read += toCopy;
        }

        return read;
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        await this.pipe.Reader.CompleteAsync();
        await this.pipe.Writer.CompleteAsync();
        this.pipe.Reset();
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var writer = this.pipe.Writer;

        var result = await writer.WriteAsync(data, cancellationToken);
    }
}
