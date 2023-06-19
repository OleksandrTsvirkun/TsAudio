using System.Runtime.InteropServices;

using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using TsAudio.Utils;

namespace TsAudio.Formats.Mp3;

public class Mp3IndexStreamWriter : IMp3IndexStreamWriter
{
    private readonly Stream stream;
    private readonly bool leaveOpen;
    private readonly Memory<byte> writeBuffer;
    private int writeBufferLength;

    public Mp3IndexStreamWriter(Stream stream, int bufferSize = 4096, bool leaveOpen = false)
    {
        if(!stream.CanWrite)
        {
            throw new ArgumentException("Provided Stream argument does not support writing.", nameof(stream));
        }

        this.stream = stream;
        this.leaveOpen = leaveOpen;
        this.writeBuffer = new byte[bufferSize];
    }

    public async ValueTask WriteAsync(IReadOnlyList<Mp3Index> indices, CancellationToken cancellationToken)
    {
        var maxStreamPosition = indices.Max(x => x.StreamPosition);
        var maxSamplePosition = indices.Max(x => x.SamplePosition);
        var maxFrameLength = indices.Max(x => x.FrameLength);
        var maxSampleCount = (ushort)indices.Max(x => x.SampleCount);

        var sampleCountDistinct = indices.DistinctBy(x => x.SampleCount).ToList();
        var frameLenghtDistinct = indices.DistinctBy(x => x.FrameLength).Select(x => x.FrameLength).ToList();

        var isConstantBitRate = sampleCountDistinct.Count == 1;

        this.WriteToBuffer(ref isConstantBitRate);

        if(isConstantBitRate)
        {
            this.WriteToBuffer(ref maxSampleCount);
        }

        var lessThan256UniqueFrameLength = frameLenghtDistinct.Count < 256;

        this.WriteToBuffer(ref lessThan256UniqueFrameLength);

        if(lessThan256UniqueFrameLength)
        {
            var uniqueFramLengthCount = (byte)frameLenghtDistinct.Count;
            this.WriteToBuffer(ref uniqueFramLengthCount);

            foreach(var frameLength in frameLenghtDistinct)
            {
                this.writeBufferLength = await this.CheckAndWriteBufferAsync(4000, cancellationToken);

                var value = (ushort)frameLength;
                this.WriteToBuffer(ref value);
            }
        }

        this.writeBufferLength = await this.CheckAndWriteBufferAsync(4000, cancellationToken);

        var maxStreamPositionType = this.GetMaxValueType(maxStreamPosition);
        var maxSamplePositionType = this.GetMaxValueType(maxSamplePosition);

        this.WriteToBuffer(ref maxStreamPositionType);
        this.WriteToBuffer(ref maxSamplePositionType);

        Action<Mp3Index> WriteFrameLenghtDelegate = lessThan256UniqueFrameLength 
                                                        ? WriteFrameLenghtIndex(frameLenghtDistinct) 
                                                        : WriteFrameLenght;

        var WriteStreamPosition = GetWriteDelegate(maxStreamPositionType, x => x.StreamPosition);
        var WriteSamplePosition = GetWriteDelegate(maxSamplePositionType, x => x.SamplePosition);

        this.writeBufferLength = await this.CheckAndWriteBufferAsync(4000, cancellationToken);

        foreach(var index in indices)
        {
            this.writeBufferLength = await this.CheckAndWriteBufferAsync(4000, cancellationToken);

            WriteStreamPosition!(index);
            WriteSamplePosition!(index);

            WriteFrameLenghtDelegate(index);

            if(!isConstantBitRate)
            {
                var value = (ushort)index.SampleCount;
                this.WriteToBuffer(ref value);
            }
        }

        await this.stream.WriteAsync(this.writeBuffer.Slice(0, this.writeBufferLength), cancellationToken);
        await this.stream.FlushAsync(cancellationToken);
    }


    private void WriteToBuffer(ref bool value)
    {
        MemoryMarshal.Write(this.writeBuffer.Span.Slice(this.writeBufferLength++), ref value);
    }

    private void WriteToBuffer(ref byte value)
    {
        this.writeBuffer.Span[this.writeBufferLength++] = value;
    }

    private void WriteToBuffer(ref MaxValueType value)
    {
        this.writeBuffer.Span[this.writeBufferLength++] = (byte)value;
    }

    private void WriteToBuffer(ref ushort value)
    {
        MemoryMarshal.Write(this.writeBuffer.Span.Slice(this.writeBufferLength), ref value);
        this.writeBufferLength += sizeof(ushort);
    }

    private void WriteToBuffer(ref int value)
    {
        MemoryMarshal.Write(this.writeBuffer.Span.Slice(this.writeBufferLength), ref value);
        this.writeBufferLength += sizeof(int);
    }

    private void WriteToBuffer(ref long value)
    {
        MemoryMarshal.Write(this.writeBuffer.Span.Slice(this.writeBufferLength), ref value);
        this.writeBufferLength += sizeof(long);
    }

    private async ValueTask<int> CheckAndWriteBufferAsync(int value, CancellationToken cancellationToken)
    {
        if(this.writeBufferLength > value)
        {
            await this.stream.WriteAsync(this.writeBuffer.Slice(0, value), cancellationToken);
            this.writeBuffer.Slice(value, this.writeBuffer.Length - value).CopyTo(this.writeBuffer);
            this.writeBufferLength = this.writeBufferLength - value;
        }

        return this.writeBufferLength;
    }

    private MaxValueType GetMaxValueType(long value)
    {
        return value < byte.MaxValue
                ? MaxValueType.@byte
                : value < ushort.MaxValue
                    ? MaxValueType.@short
                    : value < uint.MaxValue
                        ? MaxValueType.@int
                        : MaxValueType.@long;
    }

    private Action<Mp3Index> GetWriteDelegate(MaxValueType maxStreamPositionType, Func<Mp3Index, long> selector)
    {
        switch(maxStreamPositionType)
        {
            case MaxValueType.@byte:
                return (index) => {
                    var value = (byte)selector(index);
                    this.WriteToBuffer(ref value);
                };
            case MaxValueType.@short:
                return (index) => {
                    var value = (ushort)selector(index);
                    this.WriteToBuffer(ref value);
                };
            case MaxValueType.@int:
                return (index) => {
                    var value = (int)selector(index);
                    this.WriteToBuffer(ref value);
                };
            default:
                return (index) => {
                    var value = (long)selector(index);
                    this.WriteToBuffer(ref value);
                };
        }
    }

    private void WriteFrameLenght(Mp3Index index)
    {
        var value = (ushort)index.FrameLength;
        this.WriteToBuffer(ref value);
    }

    private Action<Mp3Index> WriteFrameLenghtIndex(IList<ushort> frameLengths)
    {
        return (index) =>
        {
            var frameLengthIndex = (byte)frameLengths.IndexOf(index.FrameLength);
            this.WriteToBuffer(ref frameLengthIndex);
        };
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

    private void DisposeCore()
    {
        if(this.leaveOpen)
        {
            return;
        }

        this.stream.Close();
    }
}
