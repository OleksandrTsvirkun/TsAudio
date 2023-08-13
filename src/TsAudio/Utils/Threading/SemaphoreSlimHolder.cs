using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct SemaphoreSlimHolder : IDisposable
{
    private readonly SemaphoreSlim semaphore;
    private bool disposed;

    public SemaphoreSlimHolder(SemaphoreSlim semaphore)
    {
        this.semaphore = semaphore;
    }

    public void Dispose()
    {
        if(!this.disposed)
        {
            this.semaphore.Release();
            this.disposed = true;
        }
    }
}
