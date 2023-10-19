using System;
using System.Runtime.InteropServices;

namespace TsAudio.Wave.WaveFormats;

/// <summary>
/// WaveFormatExtensible
/// http://www.microsoft.com/whdc/device/audio/multichaud.mspx
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
public class WaveFormatExtensible : WaveFormat
{
    private short wValidBitsPerSample; // bits of precision, or is wSamplesPerBlock if wBitsPerSample==0
    private int dwChannelMask; // which channels are present in stream

    /// <summary>
    /// SubFormat (may be one of AudioMediaSubtypes)
    /// </summary>
    public Guid SubFormat { get; private set; }


    /// <summary>
    /// Parameterless constructor for marshalling
    /// </summary>
    private WaveFormatExtensible()
    {
    }

    /// <summary>
    /// Creates a new WaveFormatExtensible for PCM or IEEE
    /// </summary>
    public WaveFormatExtensible(int rate, int bits, int channels)
        : base(rate, bits, channels)
    {
        this.waveFormatTag = WaveFormatEncoding.Extensible;
        this.extraSize = 22;
        this.wValidBitsPerSample = (short)bits;

        for(int n = 0; n < channels; n++)
        {
            this.dwChannelMask |= (1 << n);
        }
        if(bits == 32)
        {
            // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT
            this.SubFormat = AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT;
        }
        else
        {
            // KSDATAFORMAT_SUBTYPE_PCM
            this.SubFormat = AudioMediaSubtypes.MEDIASUBTYPE_PCM;
        }

    }

    /// <summary>
    /// WaveFormatExtensible for PCM or floating point can be awkward to work with
    /// This creates a regular WaveFormat structure representing the same audio format
    /// Returns the WaveFormat unchanged for non PCM or IEEE float
    /// </summary>
    /// <returns></returns>
    public WaveFormat ToStandardWaveFormat()
    {
        if(this.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT && bitsPerSample == 32)
        {
            return CreateIeeeFloatWaveFormat(this.sampleRate, this.channels);
        }

        if(this.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM)
        {
            return new WaveFormat(this.sampleRate, this.bitsPerSample, this.channels);
        }

        return this;
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString()
    {
        return $"WAVE_FORMAT_EXTENSIBLE {AudioMediaSubtypes.GetAudioSubtypeName(this.SubFormat)} " +
            $"{this.SampleRate}Hz {this.Channels} channels {this.BitsPerSample} bit";
    }
}
