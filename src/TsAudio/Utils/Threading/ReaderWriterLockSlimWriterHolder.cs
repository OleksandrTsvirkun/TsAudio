using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ReaderWriterLockSlimWriterHolder : IDisposable
{
    private readonly ReaderWriterLockSlim locker;
    private bool disposed;

    public ReaderWriterLockSlimWriterHolder(ReaderWriterLockSlim semaphore)
    {
        this.locker = semaphore;
    }

    void IDisposable.Dispose()
    {
        this.Dispose();
    }

    public void Dispose()
    {
        if(this.disposed)
        {
            return;
        }

        this.locker.ExitWriteLock();
        this.disposed = true;
    }
}

