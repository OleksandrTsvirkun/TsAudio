using System;
using System.Buffers;

namespace TsAudio.Utils.Streams.Http;

public class BufferSegment<T> : IDisposable
{
    public IMemoryOwner<T> MemoryOwner { get; }

    public BufferSegment<byte> Next { get; set; }

    public long Position { get; } 

    public BufferSegment(IMemoryOwner<T> memoryOwner, long position)
    {
        this.MemoryOwner = memoryOwner;
        this.Position = position;
    }

    public void Dispose()
    {
        this.MemoryOwner.Dispose();
    }
}
