using System;
using System.Buffers;

namespace TsAudio.Utils.Memory;

public struct MemoryOwner<T> : IMemoryOwner<T>
{
    private readonly IMemoryOwner<T> memoryOwner;

    public Memory<T> Memory { get; }

    public MemoryOwner(IMemoryOwner<T> memoryOwner, int length)
    {
        this.memoryOwner = memoryOwner;
        this.Memory = memoryOwner.Memory.Slice(0, length);
    }

    public MemoryOwner()
    {
        this.memoryOwner = null;
        this.Memory = Memory<T>.Empty;
    }

    public void Dispose()
    {
        this.memoryOwner?.Dispose();
    }

    public static implicit operator Memory<T>(MemoryOwner<T> memoryOwner)
    {
        return memoryOwner.Memory;
    }

    public static implicit operator ReadOnlyMemory<T>(MemoryOwner<T> memoryOwner)
    {
        return memoryOwner.Memory;
    }
}