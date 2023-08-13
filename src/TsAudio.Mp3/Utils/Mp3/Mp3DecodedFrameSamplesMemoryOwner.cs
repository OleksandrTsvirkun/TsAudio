using System;
using System.Buffers;

namespace TsAudio.Utils.Mp3;

internal class Mp3DecodedFrameSamplesMemoryOwner : IMemoryOwner<byte>
{
    private readonly Mp3DecodedFrameSamplesPool pool;
    private readonly byte[] buffer;

    internal Mp3DecodedFrameSamplesMemoryOwner(Mp3DecodedFrameSamplesPool pool, byte[] buffer)
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
