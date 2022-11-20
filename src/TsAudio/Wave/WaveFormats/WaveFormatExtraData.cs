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


        public static WaveFormat FromFormatChunk(BinaryReader reader, int formatChunkLength)
        {
            var waveFormat = new WaveFormatExtraData();

            if(formatChunkLength < 16)
            {
                throw new InvalidDataException("Invalid WaveFormat Structure");
            }

            waveFormat.waveFormatTag = (WaveFormatEncoding)reader.ReadUInt16();
            waveFormat.channels = reader.ReadInt16();
            waveFormat.sampleRate = reader.ReadInt32();
            waveFormat.averageBytesPerSecond = reader.ReadInt32();
            waveFormat.blockAlign = reader.ReadInt16();
            waveFormat.bitsPerSample = reader.ReadInt16();

            if(formatChunkLength > 16)
            {
                waveFormat.extraSize = reader.ReadInt16();
                if(waveFormat.extraSize != formatChunkLength - 18)
                {
                    Debug.WriteLine("Format chunk mismatch");
                    waveFormat.extraSize = (short)(formatChunkLength - 18);
                }
            }

            if(waveFormat.extraSize > 0)
            {
                reader.Read(waveFormat.extraData, 0, waveFormat.extraSize);
            }

            return waveFormat;
        }
    }
}
