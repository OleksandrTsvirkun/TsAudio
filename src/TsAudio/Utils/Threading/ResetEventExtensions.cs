using System.Threading;

namespace TsAudio.Utils.Threading;

public static class ResetEventExtensions
{ 
    public static ManualResetEventSlimAwaiterWithCancellation WithCancellation(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        return new ManualResetEventSlimAwaiterWithCancellation(manualResetEvent, cancellationToken);
    }

    public static ManualResetEventSlimAwaiter GetAwaiter(this ManualResetEventSlim manualResetEvent)
    {
        return new ManualResetEventSlimAwaiter(manualResetEvent);
    }
}