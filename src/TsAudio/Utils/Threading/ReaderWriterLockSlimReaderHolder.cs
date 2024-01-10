using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ReaderWriterLockSlimReaderHolder : IDisposable
{
    private readonly ReaderWriterLockSlim locker;
    private bool disposed;

    public ReaderWriterLockSlimReaderHolder(ReaderWriterLockSlim semaphore)
    {
        this.locker = semaphore;
    }

    void IDisposable.Dispose()
    {
        this.Dispose();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.locker.ExitReadLock();
        this.disposed = true;
    }
}

