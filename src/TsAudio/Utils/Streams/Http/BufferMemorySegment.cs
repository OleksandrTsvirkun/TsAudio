using System;

namespace TsAudio.Utils.Streams.Http;

public struct BufferMemorySegment<T>
{
    public ReadOnlyMemory<T> Memory { get; init; }

    public long Position { get; init; }
}
