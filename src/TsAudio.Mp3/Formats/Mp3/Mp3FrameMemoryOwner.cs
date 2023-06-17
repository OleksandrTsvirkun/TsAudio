using System;
using System.Buffers;

namespace TsAudio.Formats.Mp3;

internal class Mp3FrameMemoryOwner : IMemoryOwner<byte>
{
    private readonly Mp3FramePool pool;
    private readonly byte[] buffer;

    internal Mp3FrameMemoryOwner(Mp3FramePool pool, byte[] buffer)
    {
        this.pool = pool;
        this.buffer = buffer;
    }

    public Memory<byte> Memory => this.buffer;

    public void Dispose()
    {
        this.pool.Return(this);
    }
}
