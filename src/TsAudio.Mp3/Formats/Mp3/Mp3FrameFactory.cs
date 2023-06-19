using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Formats.Mp3;

public class Mp3FrameFactory : IMp3FrameFactory
{
    public static readonly Mp3FrameFactory Instance = new Mp3FrameFactory();

    private static readonly int[,,] bitRates = new int[,,] {
        {
            // MPEG Version 1
            { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 }, // Layer 1
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 }, // Layer 2
            { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 }, // Layer 3
        },
        {
            // MPEG Version 2 & 2.5
            { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 }, // Layer 1
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 }, // Layer 2 
            { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 }, // Layer 3 (same as layer 2)
        }
    };

    private static readonly int[,] samplesPerFrame = new int[,] {
        {   // MPEG Version 1
            384,    // Layer1
            1152,   // Layer2
            1152    // Layer3
        },
        {   // MPEG Version 2 & 2.5
            384,    // Layer1
            1152,   // Layer2
            576     // Layer3
        }
    };

    private static readonly int[] sampleRatesVersion1 = new int[] { 44100, 48000, 32000 };
    private static readonly int[] sampleRatesVersion2 = new int[] { 22050, 24000, 16000 };
    private static readonly int[] sampleRatesVersion25 = new int[] { 11025, 12000, 8000 };

    private readonly MemoryPool<byte> memoryPool;
    private readonly Mp3FramePool mp3FramePool;

    public Mp3FrameFactory(MemoryPool<byte> memoryPool = null, Mp3FramePool mp3FramePool = null)
    {
        this.memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
        this.mp3FramePool = mp3FramePool ?? new Mp3FramePool();
    }

    public async IAsyncEnumerable<Mp3FrameIndex> LoadFrameIndicesAsync(Stream input, int bufferSize = 4096, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bufferOwner = this.memoryPool.Rent(bufferSize);
        Memory<byte> memory = default;
        var samplePosition = 0L;
        var toSkip = 0;
        do
        {
            input.Position += toSkip;
            toSkip = 0;

            var read = await input.ReadAsync(bufferOwner.Memory.Slice(memory.Length), cancellationToken);

            memory = bufferOwner.Memory.Slice(0, memory.Length + read);

            while(this.TryReadFrame(ref memory, out var frame))
            {
                var streamPosition = input.Position - memory.Length - 4;

                if (memory.Length < frame.FrameLength - 4)
                {
                    toSkip = (frame.FrameLength - 4) - memory.Length;

                    memory = default;
                }
                else
                {
                    memory = memory.Slice(frame.FrameLength - 4);
                }


                var index = new Mp3Index()
                {
                    FrameLength = frame.FrameLength,
                    StreamPosition = streamPosition,
                    SamplePosition = samplePosition,
                    SampleCount = frame.SampleCount,
                };

                samplePosition += frame.SampleCount;

                yield return new Mp3FrameIndex()
                {
                    Frame = frame,
                    Index = index,  
                };
            }

            memory.CopyTo(bufferOwner.Memory);

        } while(input.Position < input.Length);

    }

    public async ValueTask<Mp3Frame?> LoadFrameAsync(Stream stream, Mp3Index index, CancellationToken cancellationToken = default)
    {
        stream.Position = index.StreamPosition;

        var originalBuffer = this.mp3FramePool.Rent(index.FrameLength);
        var buffer = originalBuffer.Memory;

        var read = await stream.ReadAsync(buffer, cancellationToken);

        if(read < index.FrameLength)
        {
            return null;
        }

        if(this.TryReadFrame(ref buffer, out var frame))
        {
            frame.RawData = originalBuffer;
            return frame;
        }

        return null;
    }

    private bool TryReadFrame(ref Memory<byte> buffer, out Mp3Frame? frame)
    {
        Span<byte> headerBytes = stackalloc byte[4];

        if(buffer.Length < 4)
        {
            frame = null;
            return false;
        }

        buffer.Span.Slice(0, 4).CopyTo(headerBytes);
        buffer = buffer.Slice(4);

        while(!this.TryParseMp3Frame(headerBytes, out frame))
        {
            if(buffer.IsEmpty)
            {
                return false;
            }

            headerBytes.Slice(1, 3).CopyTo(headerBytes);
            headerBytes[3] = buffer.Span[0];
            buffer = buffer.Slice(1);
        }

        return true;
    }

    /// <summary>
    /// checks if the four bytes represent a valid header,
    /// if they are, will parse the values into Mp3Frame
    /// </summary>
    private bool TryParseMp3Frame(ReadOnlySpan<byte> headerBytes, out Mp3Frame? frame)
    {
        frame = null;

        if((headerBytes[0] == 0xFF) && ((headerBytes[1] & 0xE0) == 0xE0))
        {
            var mpegVersion = (MpegVersion)((headerBytes[1] & 0x18) >> 3);
            if(mpegVersion == MpegVersion.Reserved)
            {
                //throw new FormatException("Unsupported MPEG Version");
                return false;
            }

            var mpegLayer = (MpegLayer)((headerBytes[1] & 0x06) >> 1);

            if(mpegLayer == MpegLayer.Reserved)
            {
                return false;
            }

            int layerIndex = mpegLayer == MpegLayer.Layer1
                                    ? 0
                                    : mpegLayer == MpegLayer.Layer2
                                        ? 1
                                        : 2;

            var CrcPresent = (headerBytes[1] & 0x01) == 0x00;
            var bitRateIndex = (headerBytes[2] & 0xF0) >> 4;
            if(bitRateIndex == 15)
            {
                // invalid index
                return false;
            }
            int versionIndex = mpegVersion == MpegVersion.Version1 ? 0 : 1;
            var bitRate = bitRates[versionIndex, layerIndex, bitRateIndex] * 1000;

            if(bitRate == 0)
            {
                return false;
            }

            int sampleFrequencyIndex = (headerBytes[2] & 0x0C) >> 2;
            if(sampleFrequencyIndex == 3)
            {
                return false;
            }

            int sampleRate;
            if(mpegVersion == MpegVersion.Version1)
            {
                sampleRate = sampleRatesVersion1[sampleFrequencyIndex];
            }
            else if(mpegVersion == MpegVersion.Version2)
            {
                sampleRate = sampleRatesVersion2[sampleFrequencyIndex];
            }
            else
            {
                // mpegVersion == MpegVersion.Version25
                sampleRate = sampleRatesVersion25[sampleFrequencyIndex];
            }

            bool padding = (headerBytes[2] & 0x02) == 0x02;
            bool privateBit = (headerBytes[2] & 0x01) == 0x01;
            var channelMode = (ChannelMode)((headerBytes[3] & 0xC0) >> 6);
            var channelExtension = (headerBytes[3] & 0x30) >> 4;
            if(channelExtension != 0 && channelMode != ChannelMode.JointStereo)
            {
                return false;
            }


            var copyright = (headerBytes[3] & 0x08) == 0x08;
            bool original = (headerBytes[3] & 0x04) == 0x04;
            int emphasis = (headerBytes[3] & 0x03);

            int nPadding = padding ? 1 : 0;

            var sampleCount = samplesPerFrame[versionIndex, layerIndex];
            int coefficient = sampleCount / 8;

            int frameLength;
            if(mpegLayer == MpegLayer.Layer1)
            {
                frameLength = (coefficient * bitRate / sampleRate + nPadding) * 4;
            }
            else
            {
                frameLength = (coefficient * bitRate) / sampleRate + nPadding;
            }

            frame = new Mp3Frame()
            {
                BitRate = bitRate,
                FrameLength = (ushort)frameLength,
                SampleCount = (ushort)sampleCount,
                BitRateIndex = bitRateIndex,
                ChannelExtension = channelExtension,
                ChannelMode = channelMode,
                Copyright = copyright,
                CrcPresent = CrcPresent,
                MpegLayer = mpegLayer,
                MpegVersion = mpegVersion,
                SampleRate = sampleRate,
            };

            return true;
        }
        return false;
    }

}
