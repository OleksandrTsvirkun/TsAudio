using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace TsAudio.Utils.Memory
{
    public class UnmanagedMemoryPool<T> : MemoryPool<T>
        where T : unmanaged
    {
        public new static UnmanagedMemoryPool<T> Shared = new UnmanagedMemoryPool<T>();

        public override int MaxBufferSize => int.MaxValue;

        public unsafe override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            if(minBufferSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minBufferSize));
            }

            var ptr = Marshal.AllocHGlobal(minBufferSize * Marshal.SizeOf<T>());
            return new UnmanagedMemoryManager<T>((T*)ptr.ToPointer(), minBufferSize);
        }

        protected override void Dispose(bool disposing)
        {
            //DO NOTHING
        }
    }
}
