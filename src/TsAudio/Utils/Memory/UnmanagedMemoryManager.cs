using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace TsAudio.Utils.Memory
{
    /// <summary>
    /// A MemoryManager over a raw pointer
    /// </summary>
    /// <remarks>The pointer is assumed to be fully unmanaged, or externally pinned - no attempt will be made to pin this data</remarks>
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T>, IUnmanagedMemoryOwner<T>
        where T : unmanaged
    {
        private readonly int length;
        private bool disposed;

        public MemoryHandle Handle { get; }

        /// <summary>
        /// Create a new UnmanagedMemoryManager instance at the given pointer and size
        /// </summary>
        public UnmanagedMemoryManager(T* pointer, int length)
        {
            if(length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            this.Handle = new MemoryHandle(pointer);
            this.length = length;
        }

        public override Span<T> GetSpan() => new Span<T>(this.Handle.Pointer, this.length);

        public override MemoryHandle Pin(int elementIndex = 0) => new MemoryHandle((T*)this.Handle.Pointer + elementIndex);

        public override void Unpin() { }

        /// <summary>
        /// Finalizer for this memory manager
        /// </summary>
        ~UnmanagedMemoryManager()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Releases resources held by this memory manager
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        /// <summary>
        /// Releases all resources associated with this object
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if(!this.disposed)
            {
                this.Handle.Dispose();
                Marshal.FreeHGlobal(new IntPtr(this.Handle.Pointer));
            }

            this.disposed = true;
        }

        public unsafe static implicit operator IntPtr(UnmanagedMemoryManager<T> obj)
        {
            return new IntPtr(obj.Handle.Pointer);
        }
    }
}
