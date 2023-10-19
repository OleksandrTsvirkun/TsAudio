using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Threading;

public static class SemaphoreExtensions
{
    public static async ValueTask<SemaphoreSlimHolder> LockAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new SemaphoreSlimHolder(semaphore);
    }
}
