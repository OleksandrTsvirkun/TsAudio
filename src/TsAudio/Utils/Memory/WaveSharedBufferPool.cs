using System;
using System.Collections.Concurrent;

using TsAudio.Utils.Memory;

namespace TsAudio.Drivers.Android.Utils;
public class WaveSharedBufferPool
{
    public static WaveSharedBufferPool Instance = new WaveSharedBufferPool();

    private readonly ConcurrentDictionary<int, ConcurrentBag<WeakReference<WaveSharedBuffer>>> pool;

    public WaveSharedBufferPool()
    {
        this.pool = new();
    }

    public WaveSharedBuffer Rent(int size)
    {
        while(this.pool.TryGetValue(size, out var items)
            && items.TryTake(out var weakRef))
        {
            if (!weakRef.TryGetTarget(out var buffer))
            {
                continue;
            }

            return buffer;
        }

        return new WaveSharedBuffer(new byte[size]);
    }

    public void Return(WaveSharedBuffer waveSharedBuffer)
    {
        var weakRef = new WeakReference<WaveSharedBuffer>(waveSharedBuffer);
        this.pool.AddOrUpdate(waveSharedBuffer.ByteBuffer.Length,
            (size) => new() {
                weakRef
            }, (size, bag) =>
            {
                bag.Add(weakRef);
                return bag;
            });
    }

    public void Clear()
    {
        this.pool.Clear();
    }
}