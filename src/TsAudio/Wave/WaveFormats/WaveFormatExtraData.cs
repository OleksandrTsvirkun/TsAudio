using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Buffers;
using System.Threading.Tasks;

namespace TsAudio.Wave.WaveFormats
{
    /// <summary>
    /// This class used for marshalling from unmanaged code
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
    public class WaveFormatExtraData : WaveFormat
    {
        // try with 100 bytes for now, increase if necessary
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        private byte[] extraData = new byte[100];

        /// <summary>
        /// Allows the extra data to be read
        /// </summary>
        public byte[] ExtraData => this.extraData; 


        public static async ValueTask<WaveFormat> FromFormatChunk(Stream reader, int formatChunkLength, CancellationToken cancellationToken = default)
        {
            if(formatChunkLength < 16)
            {
                throw new InvalidDataException("Invalid WaveFormat Structure");
            }

            using var bufferOwner = MemoryPool<byte>.Shared.Rent(16);
            var buffer = bufferOwner.Memory.Slice(0, 16);

            await reader.ReadAsync(buffer, cancellationToken);

            var waveFormatTag = (WaveFormatEncoding)BitConverter.ToUInt16(buffer.Span.Slice(0, 2)); //read 2
            var channels = BitConverter.ToInt16(buffer.Span.Slice(2, 2));      //read 2
            var sampleRate = BitConverter.ToInt32(buffer.Span.Slice(4, 4));     //read 4
            var averageBytesPerSecond = BitConverter.ToInt32(buffer.Span.Slice(8, 4)); //read 4
            var blockAlign = BitConverter.ToInt16(buffer.Span.Slice(12, 2)); //read 2
            var bitsPerSample = BitConverter.ToInt16(buffer.Span.Slice(14, 2)); //read 2

            short extraSize = 0;
            if(formatChunkLength > 16)
            {
                await reader.ReadAsync(buffer.Slice(0, 2), cancellationToken);
                extraSize = BitConverter.ToInt16(buffer.Span.Slice(0, 2));  //read 2
                if(extraSize != formatChunkLength - 16)
                {
                    extraSize = (short)(formatChunkLength - 16);
                }
            }

            var waveFormat = new WaveFormatExtraData() 
            {
                waveFormatTag = waveFormatTag,
                channels = channels,
                sampleRate = sampleRate,
                averageBytesPerSecond = averageBytesPerSecond,
                blockAlign = blockAlign,
                bitsPerSample = bitsPerSample,
                extraSize = extraSize
            };


            if(extraSize > 0)
            {
                await reader.ReadAsync(waveFormat.extraData.AsMemory(0, extraSize), cancellationToken);
            }

            return waveFormat;
        }

    }
}
