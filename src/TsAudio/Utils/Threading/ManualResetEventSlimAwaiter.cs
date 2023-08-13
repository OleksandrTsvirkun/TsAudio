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

    public void OnCompleted(Action continuation)
    {
        this.manualResetEvent.Wait();
        continuation();
    }

    public void GetResult() { }
}
