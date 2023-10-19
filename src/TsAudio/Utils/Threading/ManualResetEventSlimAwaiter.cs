using System.Runtime.CompilerServices;
using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ManualResetEventSlimAwaiter : INotifyCompletion
{
    private readonly ManualResetEventSlim manualResetEvent;

    public ManualResetEventSlimAwaiter(ManualResetEventSlim manualResetEvent)
    {
        this.manualResetEvent = manualResetEvent;
    }

    public bool IsCompleted => this.manualResetEvent.IsSet;

    public ManualResetEventSlimAwaiter GetAwaiter() => this;

    void INotifyCompletion.OnCompleted(Action continuation)
    {
        this.OnCompleted(continuation);
    }

    public void OnCompleted(Action continuation)
    {
        if (!this.manualResetEvent.IsSet)
        {
            this.manualResetEvent.Wait();
        }

        continuation();
    }

    public void GetResult() { }
}