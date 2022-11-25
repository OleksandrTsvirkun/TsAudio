using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace TsAudio.Wave.WaveFormats
{
    /// <summary>
    /// Represents a Wave file format
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi, Pack=2)]
    public class WaveFormat : IEquatable<WaveFormat>
    {
        /// <summary>
        /// Creates a WaveFormat with custom members
        /// </summary>
        /// <param name="tag">The encoding</param>
        /// <param name="sampleRate">Sample Rate</param>
        /// <param name="channels">Number of channels</param>
        /// <param name="averageBytesPerSecond">Average Bytes Per Second</param>
        /// <param name="blockAlign">Block Align</param>
        /// <param name="bitsPerSample">Bits Per Sample</param>
        /// <returns></returns>
        public static WaveFormat CreateCustomFormat(WaveFormatEncoding tag, int sampleRate, int channels, int averageBytesPerSecond, int blockAlign, int bitsPerSample)
        {
            var waveFormat = new WaveFormat();
            waveFormat.waveFormatTag = tag;
            waveFormat.channels = (short)channels;
            waveFormat.sampleRate = sampleRate;
            waveFormat.averageBytesPerSecond = averageBytesPerSecond;
            waveFormat.blockAlign = (short)blockAlign;
            waveFormat.bitsPerSample = (short)bitsPerSample;
            waveFormat.extraSize = 0;
            return waveFormat;
        }

        /// <summary>
        /// Creates an A-law wave format
        /// </summary>
        /// <param name="sampleRate">Sample Rate</param>
        /// <param name="channels">Number of Channels</param>
        /// <returns>Wave Format</returns>
        public static WaveFormat CreateALawFormat(int sampleRate, int channels)
        {
            return CreateCustomFormat(WaveFormatEncoding.ALaw, sampleRate, channels, sampleRate * channels, channels, 8);
        }

        /// <summary>
        /// Creates a Mu-law wave format
        /// </summary>
        /// <param name="sampleRate">Sample Rate</param>
        /// <param name="channels">Number of Channels</param>
        /// <returns>Wave Format</returns>
        public static WaveFormat CreateMuLawFormat(int sampleRate, int channels)
        {
            return CreateCustomFormat(WaveFormatEncoding.MuLaw, sampleRate, channels, sampleRate * channels, channels, 8);
        }

        /// <summary>
        /// Creates a new 32 bit IEEE floating point wave format
        /// </summary>
        /// <param name="sampleRate">sample rate</param>
        /// <param name="channels">number of channels</param>
        public static WaveFormat CreateIeeeFloatWaveFormat(int sampleRate, int channels)
        {
            var waveFormat = new WaveFormat();
            waveFormat.waveFormatTag = WaveFormatEncoding.IeeeFloat;
            waveFormat.channels = (short)channels;
            waveFormat.bitsPerSample = 32;
            waveFormat.sampleRate = sampleRate;
            waveFormat.blockAlign = (short)(4 * channels);
            waveFormat.averageBytesPerSecond = sampleRate * waveFormat.blockAlign;
            waveFormat.extraSize = 0;
            return waveFormat;
        }

        /// <summary>
        /// Helper function to retrieve a WaveFormat structure from a pointer
        /// </summary>
        /// <param name="pointer">WaveFormat structure</param>
        /// <returns></returns>
        public static WaveFormat MarshalFromPtr(IntPtr pointer)
        {
            var waveFormat = Marshal.PtrToStructure<WaveFormat>(pointer);
            switch(waveFormat.Encoding)
            {
                case WaveFormatEncoding.Pcm:
                    // can't rely on extra size even being there for PCM so blank it to avoid reading
                    // corrupt data
                    waveFormat.extraSize = 0;
                    break;
                case WaveFormatEncoding.Extensible:
                    waveFormat = Marshal.PtrToStructure<WaveFormatExtensible>(pointer);
                    break;
                case WaveFormatEncoding.Adpcm:
                    waveFormat = Marshal.PtrToStructure<AdpcmWaveFormat>(pointer);
                    break;
                case WaveFormatEncoding.Gsm610:
                    waveFormat = Marshal.PtrToStructure<Gsm610WaveFormat>(pointer);
                    break;
                default:
                    if(waveFormat.ExtraSize > 0)
                    {
                        waveFormat = Marshal.PtrToStructure<WaveFormatExtraData>(pointer);
                    }
                    break;
            }
            return waveFormat;
        }

        /// <summary>
        /// Helper function to marshal WaveFormat to an IntPtr
        /// </summary>
        /// <param name="format">WaveFormat</param>
        /// <returns>IntPtr to WaveFormat structure (needs to be freed by callee)</returns>
        public static IntPtr MarshalToPtr(WaveFormat format)
        {
            int formatSize = Marshal.SizeOf(format);
            IntPtr formatPointer = Marshal.AllocHGlobal(formatSize);
            Marshal.StructureToPtr(format, formatPointer, false);
            return formatPointer;
        }

        /// <summary>format type</summary>
        protected WaveFormatEncoding waveFormatTag;
        /// <summary>number of channels</summary>
        protected short channels;
        /// <summary>sample rate</summary>
        protected int sampleRate;
        /// <summary>for buffer estimation</summary>
        protected int averageBytesPerSecond;
        /// <summary>block size of data</summary>
        protected short blockAlign;
        /// <summary>number of bits per sample of mono data</summary>
        protected short bitsPerSample;
        /// <summary>number of following bytes</summary>
        protected short extraSize;

        /// <summary>
        /// Returns the number of channels (1=mono,2=stereo etc)
        /// </summary>
        public int Channels => this.channels;

        /// <summary>
        /// Returns the sample rate (samples per second)
        /// </summary>
        public int SampleRate => this.sampleRate;

        /// <summary>
        /// Returns the average number of bytes used per second
        /// </summary>
        public int AverageBytesPerSecond => this.averageBytesPerSecond;

        /// <summary>
        /// Returns the block alignment
        /// </summary>
        public virtual int BlockAlign => this.blockAlign;

        /// <summary>
        /// Returns the number of bits per sample (usually 16 or 32, sometimes 24 or 8)
        /// Can be 0 for some codecs
        /// </summary>
        public int BitsPerSample => this.bitsPerSample;

        /// <summary>
        /// Returns the number of extra bytes used by this waveformat. Often 0,
        /// except for compressed formats which store extra data after the WAVEFORMATEX header
        /// </summary>
        public int ExtraSize => this.extraSize;


        /// <summary>
        /// Returns the encoding type used
        /// </summary>
        public WaveFormatEncoding Encoding => this.waveFormatTag;

        /// <summary>
        /// Creates a new PCM 44.1Khz stereo 16 bit format
        /// </summary>
        public WaveFormat() : this(44100,16,2)
        {

        }
        
        /// <summary>
        /// Creates a new 16 bit wave format with the specified sample
        /// rate and channel count
        /// </summary>
        /// <param name="sampleRate">Sample Rate</param>
        /// <param name="channels">Number of channels</param>
        public WaveFormat(int sampleRate, int channels) : this(sampleRate, 16, channels)
        {
        }

        /// <summary>
        /// Creates a new PCM format with the specified sample rate, bit depth and channels
        /// </summary>
        public WaveFormat(int rate, int bits, int channels)
        {
            if (channels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be 1 or greater");
            }
            // minimum 16 bytes, sometimes 18 for PCM
            this.waveFormatTag = WaveFormatEncoding.Pcm;
            this.channels = (short)channels;
            this.sampleRate = rate;
            this.bitsPerSample = (short)bits;
            this.extraSize = 0;

            this.blockAlign = (short)(channels * (bits / 8));
            this.averageBytesPerSecond = this.sampleRate * this.blockAlign;
        }

        /// <summary>
        /// Gets the size of a wave buffer equivalent to the latency in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The milliseconds.</param>
        /// <returns></returns>
        public int ConvertLatencyToByteSize(int milliseconds)
        {
            int bytes = (int)((this.AverageBytesPerSecond / 1000.0) * milliseconds);
            if((bytes % this.BlockAlign) != 0)
            {
                // Return the upper BlockAligned
                bytes = bytes + this.BlockAlign - (bytes % this.BlockAlign);
            }
            return bytes;
        }

        /// <summary>
        /// Reports this WaveFormat as a string
        /// </summary>
        /// <returns>String describing the wave format</returns>
        public override string ToString()
        {
            switch (this.waveFormatTag)
            {
                case WaveFormatEncoding.Pcm:
                case WaveFormatEncoding.Extensible:
                    // extensible just has some extra bits after the PCM header
                    return $"{this.bitsPerSample} bit PCM: {this.sampleRate}Hz {this.channels} channels";
                case WaveFormatEncoding.IeeeFloat:
                    return $"{this.bitsPerSample} bit IEEFloat: {this.sampleRate}Hz {this.channels} channels";
                default:
                    return this.waveFormatTag.ToString();
            }
        }

        /// <summary>
        /// Compares with another WaveFormat object
        /// </summary>
        /// <param name="obj">Object to compare to</param>
        /// <returns>True if the objects are the same</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as WaveFormat);
        }

        /// <summary>
        /// Compares with another WaveFormat object
        /// </summary>
        /// <param name="other">Object to compare to</param>
        /// <returns>True if the objects are the same</returns>
        public bool Equals(WaveFormat other)
        {
            if (other is null)
            {
                return false;
            }

            return this.waveFormatTag == other.waveFormatTag &&
                this.channels == other.channels &&
                this.sampleRate == other.sampleRate &&
                this.averageBytesPerSecond == other.averageBytesPerSecond &&
                this.blockAlign == other.blockAlign &&
                this.bitsPerSample == other.bitsPerSample;
        }

        /// <summary>
        /// Provides a Hashcode for this WaveFormat
        /// </summary>
        /// <returns>A hashcode</returns>
        public override int GetHashCode()
        {
            return (int)this.waveFormatTag ^ 
                (int)this.channels ^
                this.sampleRate ^
                this.averageBytesPerSecond ^ 
                (int)this.blockAlign ^ 
                (int)this.bitsPerSample;
        }
    }
}
