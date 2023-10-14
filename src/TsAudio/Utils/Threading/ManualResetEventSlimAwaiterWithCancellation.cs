using System.Runtime.CompilerServices;
using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ManualResetEventSlimAwaiterWithCancellation : INotifyCompletion
{
    private readonly ManualResetEventSlim manualResetEvent;
    private readonly CancellationToken cancellationToken;

    public ManualResetEventSlimAwaiterWithCancellation(ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        this.manualResetEvent = manualResetEvent;
        this.cancellationToken = cancellationToken;
    }

    public bool IsCompleted => this.manualResetEvent.IsSet;

    public ManualResetEventSlimAwaiterWithCancellation GetAwaiter() => this;

    void INotifyCompletion.OnCompleted(Action continuation)
    {
        this.OnCompleted(continuation);
    }

    public void OnCompleted(Action continuation)
    {
        try
        {
            this.manualResetEvent.Wait(this.cancellationToken);
        }
        catch(OperationCanceledException ex)
        {
            this.manualResetEvent.Set();
        }
        finally
        {
            continuation();
        }
    }

    public void GetResult() { }
}