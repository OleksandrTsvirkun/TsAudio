using System.Buffers;

namespace TsAudio.Utils.Memory;

public static class MemoryOwnerExtensions
{
    public static MemoryOwner<T> Exact<T>(this IMemoryOwner<T> memoryOwner, int length)
    {
        return new MemoryOwner<T>(memoryOwner, length);
    }
}
