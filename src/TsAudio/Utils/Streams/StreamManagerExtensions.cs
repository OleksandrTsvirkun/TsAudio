using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public static class StreamManagerExtensions
{
    public static Task LoadAsync(this IBufferedStreamManager streamManager, Stream stream, MemoryPool<byte> memoryPool = null, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        return Task.Run(LoadAsyncImpl, cancellationToken);

        async Task LoadAsyncImpl()
        {
            memoryPool ??= MemoryPool<byte>.Shared;

            using var memoryOwner = memoryPool.Rent(bufferSize);

            while(!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(memoryOwner.Memory, cancellationToken);

                if(read == 0)
                {
                    break;
                }

                await streamManager.WriteAsync(memoryOwner.Memory.Slice(0, read), cancellationToken);
            }

            await streamManager.FlushAsync(cancellationToken);
        }
    }
}
