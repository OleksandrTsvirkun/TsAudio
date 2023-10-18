using System.Threading;

namespace TsAudio.Utils.Threading;

public static class ResetEventExtensions
{
    public static ManualResetEventSlimAwaiterWithCancellation ResetAndGetAwaiterWithCancellation(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        manualResetEvent.Reset();
        return new ManualResetEventSlimAwaiterWithCancellation(manualResetEvent, cancellationToken);
    }

    public static ManualResetEventSlimAwaiterWithCancellation GetAwaiterWithCancellation(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
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