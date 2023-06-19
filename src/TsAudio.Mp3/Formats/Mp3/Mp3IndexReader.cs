using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils;

namespace TsAudio.Formats.Mp3;

public class Mp3IndexStreamReader : IMp3IndexStreamReader
{
    private readonly Stream stream;
    private readonly int bufferSize;
    private readonly bool leaveOpen;
    private readonly MemoryPool<byte> memoryPool;
    private IMemoryOwner<byte> bufferOwner;
    private bool allDataBuffered;

    public Mp3IndexStreamReader(Stream stream, int bufferSize = 4096, MemoryPool<byte> memoryPool = null, bool leaveOpen = false)
    {
        this.stream = stream;
        this.bufferSize = bufferSize;
        this.leaveOpen = leaveOpen;
        this.memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
        this.bufferOwner = this.memoryPool.Rent(bufferSize);
        this.allDataBuffered = false;
    }

    public async IAsyncEnumerator<Mp3Index> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        ushort? sampleCount = null;
        IList<ushort> uniqueFrameLength = null!;

        var memory = this.bufferOwner.Memory;

        var read = await this.stream.ReadAsync(memory, cancellationToken);
        memory = memory.Slice(0, read);

        var isSampleCountFixed = ReadBoolean(ref memory);

        if(isSampleCountFixed)
        {
            sampleCount = ReadInt16(ref memory);
        }

        var isUniqueFrameLengthLessThan256 = ReadBoolean(ref memory);

        if(isUniqueFrameLengthLessThan256)
        {
            var uniqueFrameLengthCount = ReadByte(ref memory);

            uniqueFrameLength = new List<ushort>(uniqueFrameLengthCount);

            for(int i = 0; i < uniqueFrameLengthCount; i++)
            {
                memory = await this.CheckAndRotateBufferAsync(memory, 64, cancellationToken);

                var frameLength = ReadInt16(ref memory);

                uniqueFrameLength.Add(frameLength);
            }
        }

        var maxStreamPositionType = (MaxValueType)ReadByte(ref memory);
        var maxSamplePositionType = (MaxValueType)ReadByte(ref memory);

        var ReadStreamPosition = GetReadDelegate(maxStreamPositionType);
        var ReadSamplePosition = GetReadDelegate(maxSamplePositionType);

        Func<ushort> ReadFrameLengthDelegate = isUniqueFrameLengthLessThan256
                                    ? ReadFrameLengthIndex
                                    : ReadFrameLength;

        Func<ushort> ReadSampleCountDelegate = sampleCount.HasValue
                                        ? () => sampleCount.Value
                                        : ReadSampleCount;

        while(memory.Length > 0 || !this.allDataBuffered)
        {
            Mp3Index index;
            try
            {
                memory = await this.CheckAndRotateBufferAsync(memory, 128, cancellationToken);

                var streamPosition = ReadStreamPosition();
                var samplePosition = ReadSamplePosition();
                var frameLength = ReadFrameLengthDelegate();
                var sampleCountVal = ReadSampleCountDelegate();

                index = new Mp3Index()
                {
                    SampleCount = sampleCountVal,
                    FrameLength = frameLength,
                    SamplePosition = samplePosition,
                    StreamPosition = streamPosition
                };
            }
            catch(ArgumentOutOfRangeException ex)
            {
                break;
            }
            catch(EndOfStreamException ex)
            {
                break;
            }

            yield return index;
        }


        Func<long> GetReadDelegate(MaxValueType maxStreamPositionType)
        {
            switch(maxStreamPositionType)
            {
                case MaxValueType.@byte:
                    return () => ReadByte(ref memory);
                case MaxValueType.@short:
                    return () => ReadInt16(ref memory);
                case MaxValueType.@int:
                    return () => ReadInt32(ref memory);
                default:
                    return () => ReadInt64(ref memory);
            }
        }

        ushort ReadFrameLengthIndex()
        {
            var index = ReadByte(ref memory);
            return uniqueFrameLength![index];
        };

        ushort ReadFrameLength()
        {
            return ReadInt16(ref memory);
        }

        ushort ReadSampleCount()
        {
            return ReadInt16(ref memory);
        }
    }

    public void Dispose()
    {
        this.DisposeCore();
        this.stream.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        this.DisposeCore();
        await this.stream.DisposeAsync();
    }

    private static byte ReadByte(ref Memory<byte> memory)
    {
        var value = memory.Span.Slice(0, 1)[0];
        memory = memory.Slice(1);
        return value;
    }

    private static bool ReadBoolean(ref Memory<byte> memory)
    {
        var size = sizeof(bool);
        var value = BitConverter.ToBoolean(memory.Span.Slice(0, size));
        memory = memory.Slice(size);
        return value;
    }

    private static ushort ReadInt16(ref Memory<byte> memory)
    {
        var size = sizeof(ushort);
        var value = BitConverter.ToUInt16(memory.Span.Slice(0, size));
        memory = memory.Slice(size);
        return value;
    }

    private static int ReadInt32(ref Memory<byte> memory)
    {
        var size = sizeof(int);
        var value = BitConverter.ToInt32(memory.Span.Slice(0, size));
        memory = memory.Slice(size);
        return value;
    }

    private static long ReadInt64(ref Memory<byte> memory)
    {
        var size = sizeof(int);
        var value = BitConverter.ToInt64(memory.Span.Slice(0, size));
        memory = memory.Slice(size);
        return value;
    }

    private async ValueTask<Memory<byte>> CheckAndRotateBufferAsync(Memory<byte> memory, int value, CancellationToken cancellationToken)
    {
        if(memory.Length < value)
        {
            var newBuffer = this.memoryPool.Rent(this.bufferSize);
            memory.CopyTo(newBuffer.Memory);
            var read = await this.stream.ReadAsync(newBuffer.Memory.Slice(memory.Length), cancellationToken);

            if (read == 0)
            {
                this.allDataBuffered = true;
            }

            var oldBuffer = this.bufferOwner;
            this.bufferOwner = newBuffer;
            memory = this.bufferOwner.Memory.Slice(0, read + memory.Length);
            oldBuffer.Dispose();
        }

        return memory;
    }



    private void DisposeCore()
    {
        this.bufferOwner.Dispose();

        if(this.leaveOpen)
        {
            return;
        }

        this.stream.Close();
    }
}
