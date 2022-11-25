using System;
using System.Runtime.InteropServices;
using System.IO;
using TsAudio.Wave.WaveFormats;
using System.Diagnostics;

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

        //TODO: reimplement without binary formatter
        public static WaveFormat FromFormatChunk(Stream stream, int formatChunkLength)
        {
            var waveFormat = new WaveFormatExtraData();

            if(formatChunkLength < 16)
            {
                throw new InvalidDataException("Invalid WaveFormat Structure");
            }

            Span<byte> buffer = stackalloc byte[2 + 2 + 4 + 4 + 2 + 2 + 2];

            stream.Read(buffer);

            waveFormat.waveFormatTag = MemoryMarshal.Read<WaveFormatEncoding>(buffer.Slice(0, 2));
            waveFormat.channels = MemoryMarshal.Read<short>(buffer.Slice(2, 2));
            waveFormat.sampleRate = MemoryMarshal.Read<short>(buffer.Slice(4, 4));
            waveFormat.averageBytesPerSecond = MemoryMarshal.Read<short>(buffer.Slice(8, 4));
            waveFormat.blockAlign = MemoryMarshal.Read<short>(buffer.Slice(12, 2));
            waveFormat.bitsPerSample = MemoryMarshal.Read<short>(buffer.Slice(14, 2));

            if(formatChunkLength > 16)
            {
                Span<byte> span = stackalloc byte[2];

                stream.Read(span);

                waveFormat.extraSize = MemoryMarshal.Read<short>(span);

                if(waveFormat.extraSize != formatChunkLength - 18)
                {
                    Debug.WriteLine("Format chunk mismatch");
                    waveFormat.extraSize = (short)(formatChunkLength - 18);
                }
            }

            if(waveFormat.extraSize > 0)
            {
                stream.Read(waveFormat.extraData);
            }

            return waveFormat;
        }
    }
}
