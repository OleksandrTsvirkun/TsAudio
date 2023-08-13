using System;
using System.Buffers;
using System.Collections.Concurrent;

namespace TsAudio.Utils.Mp3;

public class Mp3FrameMemoryPool : MemoryPool<byte>
{
    public static Mp3FrameMemoryPool Instance = new Mp3FrameMemoryPool();

    private readonly ConcurrentDictionary<int, ConcurrentBag<WeakReference<Mp3FrameMemoryOwner>>> pool;

    public Mp3FrameMemoryPool()
    {
        this.pool = new ConcurrentDictionary<int, ConcurrentBag<WeakReference<Mp3FrameMemoryOwner>>>();
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

        return new Mp3FrameMemoryOwner(this, new byte[minBufferSize]);
    }

    internal void Return(Mp3FrameMemoryOwner frame)
    {
        this.pool.AddOrUpdate(frame.Memory.Length, (length) =>
        {
            return new ConcurrentBag<WeakReference<Mp3FrameMemoryOwner>>()
            {
                new WeakReference<Mp3FrameMemoryOwner>(frame)
            };
        }, (length, frames) =>
        {
            frames.Add(new WeakReference<Mp3FrameMemoryOwner>(frame));
            return frames;
        });
    }

    protected override void Dispose(bool disposing)
    {
        this.pool.Clear();
    }
}
