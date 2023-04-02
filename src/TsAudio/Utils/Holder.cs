using System;
using System.Threading;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Utils;

public struct Holder : IDisposable
{
    private readonly SemaphoreSlim semaphore;
    private bool disposed;

    public Holder(SemaphoreSlim semaphore)
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
