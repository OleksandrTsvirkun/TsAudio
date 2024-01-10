using System.Runtime.CompilerServices;
using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ManualResetEventSlimAwaiterWithCancellation : INotifyCompletion
{
    private readonly ManualResetEventSlim manualResetEvent;
    private readonly CancellationToken cancellationToken;
    private readonly CancellationTokenRegistration cancellationTokenRegistration; 

    public ManualResetEventSlimAwaiterWithCancellation(ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken)
    {
        this.manualResetEvent = manualResetEvent;
        this.cancellationToken = cancellationToken;
        this.cancellationTokenRegistration = this.cancellationToken.Register(this.manualResetEvent.Set);
    }

    public bool IsCompleted => this.manualResetEvent.IsSet;

    public ManualResetEventSlimAwaiterWithCancellation GetAwaiter() => this;

    void INotifyCompletion.OnCompleted(Action continuation)
    {
        this.OnCompleted(continuation);
    }

    public void OnCompleted(Action continuation)
    {
        if(!this.manualResetEvent.IsSet)
        {
            this.manualResetEvent.Wait();
        }
        continuation();
        this.cancellationTokenRegistration.Dispose();
    }

    public void GetResult() { }
}