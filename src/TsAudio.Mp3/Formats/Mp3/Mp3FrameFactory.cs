using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Utils;
using TsAudio.Utils.Memory;

namespace TsAudio.Formats.Mp3
{
    public class Mp3FrameFactory : IMp3FrameFactory
    {
        enum ParsingState
        {
            ReadingHeader,
            ReadingFrameData,
            ReturnFrame
        }

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

        private readonly MemoryPool<byte> pool;

        public Mp3FrameFactory(MemoryPool<byte> memoryPool = null)
        {
            this.pool = memoryPool ?? MemoryPool<byte>.Shared;
        }

        public async IAsyncEnumerable<Mp3Frame> LoadFramesAsync(Stream input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var pipeReader = PipeReader.Create(input, new StreamPipeReaderOptions(leaveOpen: true));

            Memory<byte> raw = Memory<byte>.Empty;
            Mp3Frame? frame = null;
            ParsingState state = ParsingState.ReadingHeader;

            while(true)
            {
                var result = await pipeReader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                while(!buffer.IsEmpty)
                {
                    if(state == ParsingState.ReadingHeader)
                    {
                        frame = this.ParseFrameHeader(ref buffer);
                        if(!frame.Equals(default))
                        {
                            state = ParsingState.ReadingFrameData;
                        }
                        else if(buffer.Length < 4)
                        {
                            pipeReader.AdvanceTo(buffer.Start, buffer.End);
                            break;
                        }
                    }
                    else if(state == ParsingState.ReadingFrameData)
                    {
                        ReadFrameData(ref raw, in frame, ref state, ref buffer);
                    }
                    else if(state == ParsingState.ReturnFrame)
                    {
                        state = ParsingState.ReadingHeader;
                        yield return frame.Value;
                    }

                    pipeReader.AdvanceTo(buffer.Start, buffer.End);
                }

                if(result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete.
            await pipeReader.CompleteAsync();
        }

        public async IAsyncEnumerable<(Mp3Index Index, Mp3Frame Frame)> LoadFrameHeadersAsync(Stream input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var pipeReader = PipeReader.Create(input, new StreamPipeReaderOptions(leaveOpen: true));
            
            long rawDataSize = 0;
            Mp3Frame? frame = null;
            ParsingState state = ParsingState.ReadingHeader;
            long samples = 0;

            while(true)
            {
                var result = await pipeReader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                while(!buffer.IsEmpty)
                {
                    if(state == ParsingState.ReadingHeader)
                    {
                        frame = this.ParseFrameHeader(ref buffer);
                        if(frame.HasValue)
                        {
                            state = ParsingState.ReadingFrameData;
                        }
                        else if(buffer.Length < 4)
                        {
                            pipeReader.AdvanceTo(buffer.Start, buffer.End);
                            break;
                        }
                    }
                    else if(state == ParsingState.ReadingFrameData)
                    {
                        SkipFrameData(ref rawDataSize, in frame, ref state, ref buffer);
                    }
                    else if(state == ParsingState.ReturnFrame)
                    {
                        state = ParsingState.ReadingHeader;

                        var index = new Mp3Index()
                        {
                            FrameLength = frame.Value.FrameLength,
                            SampleCount = frame.Value.SampleCount,
                            SamplePosition = samples,
                            StreamPosition = input.Position - buffer.Length - frame.Value.FrameLength,
                        };

                        samples += frame.Value.SampleCount;

                        yield return (index, frame.Value);
                    }
                    
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);
                }

                if(result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete.
            await pipeReader.CompleteAsync();
        }

        public async ValueTask<Mp3Frame?> LoadFrameAsync(Stream stream, Mp3Index index, CancellationToken cancellationToken = default)
        {
            stream.Position = index.StreamPosition;

            var bufferOwner = this.pool.Rent(index.FrameLength);
            var buffer = bufferOwner.Memory.Slice(0, index.FrameLength);

            var read = await stream.ReadAsync(buffer, cancellationToken);

            if(read < index.FrameLength)
            {
                return null;
            }

            return ParseFrame(buffer, true);
        }

        private void ReadFrameData(ref Memory<byte> raw, in Mp3Frame? frame, ref ParsingState state, ref ReadOnlySequence<byte> buffer)
        {
            if(raw.Equals(Memory<byte>.Empty))
            {
                raw = frame.Value.RawData.Memory.Slice(4);
            }

            this.ReadFrameRawData(ref buffer, ref raw);

            if(raw.Length == 0)
            {
                raw = Memory<byte>.Empty;
                state = ParsingState.ReturnFrame;
            }
        }

        private void SkipFrameData(ref long rawDataSize, in Mp3Frame? frame, ref ParsingState state, ref ReadOnlySequence<byte> buffer)
        {
            if(rawDataSize == 0)
            {
                rawDataSize = frame.Value.RawData.Memory.Length - 4;
            }

            var toCopy = (int)Math.Min(buffer.Length, rawDataSize);
            buffer = buffer.Slice(toCopy);
            rawDataSize -= toCopy;

            if(rawDataSize == 0)
            {
                state = ParsingState.ReturnFrame;
            }
        }

        private void ReadFrameRawData(ref ReadOnlySequence<byte> buffer, ref Memory<byte> rawData)
        {
            var toCopy = (int)Math.Min(buffer.Length, rawData.Length);
            buffer.Slice(0, toCopy).CopyTo(rawData.Span.Slice(0, toCopy));
            buffer = buffer.Slice(toCopy);
            rawData = rawData.Slice(toCopy);
        }

        private Mp3Frame? ParseFrameHeader(ref ReadOnlySequence<byte> buffer, bool readData = true)
        {
            Span<byte> headerBytes = stackalloc byte[4];
            Mp3Frame frame = default;

            if(buffer.Length < 4)
            {
                return null;
            }

            buffer.Slice(0, 4).CopyTo(headerBytes);
            buffer = buffer.Slice(4);

            while(!this.TryParseMp3Frame(headerBytes, ref frame))
            {
                if(buffer.IsEmpty)
                {
                    return null;
                }

                headerBytes.Slice(1, 3).CopyTo(headerBytes);
                headerBytes[3] = buffer.FirstSpan[0];
                buffer = buffer.Slice(1);
            }

            if(readData)
            {
                var memoryOwner = this.pool.Rent(frame.FrameLength);
                frame.RawData = new MemoryOwner<byte>(memoryOwner, frame.FrameLength);
                headerBytes.CopyTo(frame.RawData.Memory.Span);
            }

            return frame;
        }

        private Mp3Frame? ParseFrame(ReadOnlyMemory<byte> memory, bool readData = false)
        {
            var buffer = new ReadOnlySequence<byte>(memory);
            var frame = this.ParseFrameHeader(ref buffer, readData);

            if(!frame.HasValue)
            {
                return default;
            }

            var raw = frame.Value.RawData.Memory.Slice(4);

            this.ReadFrameRawData(ref buffer, ref raw);

            return frame;
        }

        /// <summary>
        /// checks if the four bytes represent a valid header,
        /// if they are, will parse the values into Mp3Frame
        /// </summary>
        private bool TryParseMp3Frame(ReadOnlySpan<byte> headerBytes, ref Mp3Frame frame)
        {
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
                    FrameLength = frameLength,
                    SampleCount = sampleCount,
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
}
