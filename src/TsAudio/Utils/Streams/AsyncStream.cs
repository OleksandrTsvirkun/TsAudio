using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;
public abstract class AsyncStream : Stream
{
    public sealed override void Flush()
    {
        throw new NotImplementedException();
    }

    public sealed override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public sealed override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public abstract override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);

    public abstract override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);

    public abstract override Task FlushAsync(CancellationToken cancellationToken);
}
