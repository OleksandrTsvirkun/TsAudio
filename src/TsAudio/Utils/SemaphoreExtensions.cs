using System.Threading;
using System.Threading.Tasks;
using TsAudio.Utils;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Utils;

public static class SemaphoreExtensions
{
    public static async ValueTask<Holder> LockAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new Holder(semaphore);   
    } 
}
