using System.Buffers;

namespace TsAudio.Utils.Memory
{
    public interface IUnmanagedMemoryOwner<T> : IMemoryOwner<T>
        where T : unmanaged
    {
        MemoryHandle Handle { get; }
    }
}
