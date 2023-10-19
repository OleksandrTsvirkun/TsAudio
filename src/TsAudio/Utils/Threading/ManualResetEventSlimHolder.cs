using System;
using System.Threading;

namespace TsAudio.Utils.Threading;

public struct ManualResetEventSlimHolder : IDisposable
{
    private readonly ManualResetEventSlim manualResetEvent;
    private bool disposed;

    public ManualResetEventSlimHolder(ManualResetEventSlim manualResetEvent)
    {
        this.manualResetEvent = manualResetEvent;
    }

    void IDisposable.Dispose()
    {
        this.Dispose();
    }

    public void Dispose()
    {
        if(!this.disposed)
        {
            this.manualResetEvent.Set();
            this.disposed = true;
        }
    }
}
