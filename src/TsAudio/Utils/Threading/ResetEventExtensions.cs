using System.Threading;

namespace TsAudio.Utils.Threading;

public static class ResetEventExtensions
{
    public static ReaderWriterLockSlimReaderHolder AquireReadLock(this ReaderWriterLockSlim locker)
    {
        locker.EnterReadLock();
        return new (locker);
    }

    public static ReaderWriterLockSlimWriterHolder AquireWriteLock(this ReaderWriterLockSlim locker)
    {
        locker.EnterWriteLock();
        return new (locker);
    }


    public static ManualResetEventSlimAwaiterWithCancellation ResetAndGetAwaiterWithSoftCancellation(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        manualResetEvent.Reset();
        return new ManualResetEventSlimAwaiterWithCancellation(manualResetEvent, cancellationToken);
    }

    public static ManualResetEventSlimAwaiterWithCancellation GetAwaiterWithSoftCancellation(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        return new ManualResetEventSlimAwaiterWithCancellation(manualResetEvent, cancellationToken);
    }

    public static ManualResetEventSlimAwaiter GetAwaiter(this ManualResetEventSlim manualResetEvent)
    {
        return new ManualResetEventSlimAwaiter(manualResetEvent);
    }

    public static ManualResetEventSlimHolder Lock(this ManualResetEventSlim manualResetEvent)
    {
        manualResetEvent.Reset();
        return new ManualResetEventSlimHolder(manualResetEvent);
    }
}