using System;
using System.Buffers;
using System.Collections.Concurrent;

namespace TsAudio.Utils.Mp3;

public class Mp3DecodedFrameSamplesPool : MemoryPool<byte>
{
    public static new Mp3DecodedFrameSamplesPool Shared = new Mp3DecodedFrameSamplesPool();

    private readonly ConcurrentDictionary<int, ConcurrentBag<WeakReference<Mp3DecodedFrameSamplesMemoryOwner>>> pool;

    public Mp3DecodedFrameSamplesPool()
    {
        this.pool = new();
    }

    public override int MaxBufferSize => ushort.MaxValue;

    public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
    {
        if(this.pool.TryGetValue(minBufferSize, out var frames)
            && frames.TryTake(out var reference)
            && reference.TryGetTarget(out var frame)
            && frame is not null
            && frame.Memory.Length > 0)
        {
            return frame;
        }

        return new Mp3DecodedFrameSamplesMemoryOwner(this, new byte[minBufferSize]);
    }

    internal void Return(Mp3DecodedFrameSamplesMemoryOwner frame)
    {
        this.pool.AddOrUpdate(frame.Memory.Length, (length) =>
        {
            return new ConcurrentBag<WeakReference<Mp3DecodedFrameSamplesMemoryOwner>>()
            {
                new WeakReference<Mp3DecodedFrameSamplesMemoryOwner>(frame)
            };
        }, (length, frames) =>
        {
            frames.Add(new WeakReference<Mp3DecodedFrameSamplesMemoryOwner>(frame));
            return frames;
        });
    }

    protected override void Dispose(bool disposing)
    {
        this.pool.Clear();
    }
}
