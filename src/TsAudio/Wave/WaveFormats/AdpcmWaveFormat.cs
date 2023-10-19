using System;
using System.Runtime.InteropServices;

namespace TsAudio.Wave.WaveFormats;

/// <summary>
/// Microsoft ADPCM
/// See http://icculus.org/SDL_sound/downloads/external_documentation/wavecomp.htm
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack=2)]
public class AdpcmWaveFormat : WaveFormat
{
    private short samplesPerBlock;
    private short numCoeff;

    // 7 pairs of coefficients
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
    private short[] coefficients;

    /// <summary>
    /// Samples per block
    /// </summary>
    public int SamplesPerBlock => this.samplesPerBlock;

    /// <summary>
    /// Number of coefficients
    /// </summary>
    public int NumCoefficients => this.numCoeff;

    /// <summary>
    /// Coefficients
    /// </summary>
    public short[] Coefficients => this.coefficients;


    /// <summary>
    /// Empty constructor needed for marshalling from a pointer
    /// </summary>
    AdpcmWaveFormat() : this(8000,1)
    {
    }


    /// <summary>
    /// Microsoft ADPCM  
    /// </summary>
    /// <param name="sampleRate">Sample Rate</param>
    /// <param name="channels">Channels</param>
    public AdpcmWaveFormat(int sampleRate, int channels) : base(sampleRate,0,channels)
    {
        this.waveFormatTag = WaveFormatEncoding.Adpcm;
        
        // TODO: validate sampleRate, bitsPerSample
        this.extraSize = 32;

        switch(this.sampleRate)
        {
            case 8000: 
            case 11025:
                this.blockAlign = 256; 
                break;
            case 22050:
                this.blockAlign = 512;
                break;
            case 44100:
            default:
                this.blockAlign = 1024;
                break;
        }

        this.bitsPerSample = 4;
        this.samplesPerBlock = (short)((((this.blockAlign - (7 * channels)) * 8) / (this.bitsPerSample * channels)) + 2);
        this.averageBytesPerSecond = (this.SampleRate * this.blockAlign) / this.samplesPerBlock;

        this.numCoeff = 7;
        this.coefficients = new short[14] {
            256,0,512,-256,0,0,192,64,240,0,460,-208,392,-232
        };
    }

    /// <summary>
    /// String Description of this WaveFormat
    /// </summary>
    public override string ToString()
    {
        return string.Format("Microsoft ADPCM {0} Hz {1} channels {2} bits per sample {3} samples per block",
            this.SampleRate, this.Channels, this.BitsPerSample, this.SamplesPerBlock);
    }
}
