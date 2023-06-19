using PooledAwait;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Threading;
public static class ResetEventExtensions
{
    public static ValueTask WaitAsync(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken = default)
    {
        manualResetEvent.Wait(cancellationToken);
        return ValueTask.CompletedTask;
    }
}
