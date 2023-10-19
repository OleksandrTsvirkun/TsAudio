using System;
using System.Buffers;

namespace TsAudio.Utils.Mp3;

internal class Mp3FrameMemoryOwner : IMemoryOwner<byte>
{
    private readonly Mp3FrameMemoryPool pool;
    private readonly byte[] buffer;

    internal Mp3FrameMemoryOwner(Mp3FrameMemoryPool pool, byte[] buffer)
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
