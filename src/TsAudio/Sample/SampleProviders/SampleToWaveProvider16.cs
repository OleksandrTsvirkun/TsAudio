using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TsAudio.Wave.WaveFormats;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Converts a sample provider to 16 bit PCM, optionally clipping and adjusting volume along the way
/// </summary>
public class SampleToWaveProvider16 : SampleToWaveProviderBase
{
    public override WaveFormat WaveFormat { get; }

    /// <summary>
    /// Converts from an ISampleProvider (IEEE float) to a 16 bit PCM IWaveProvider.
    /// Number of channels and sample rate remain unchanged.
    /// </summary>
    /// <param name="sourceProvider">The input source provider</param>
    public SampleToWaveProvider16(ISampleProvider sourceProvider) : base(sourceProvider, 2)
    {  
        this.WaveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 16, sourceProvider.WaveFormat.Channels);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void TransformSamples(Span<byte> buffer, ReadOnlySpan<float> sampleBuffer)
    {
        var destWaveBuffer = MemoryMarshal.Cast<byte, short>(buffer);
        for(int sample = 0, destOffset = 0; sample < sampleBuffer.Length; sample++)
        {
            destWaveBuffer[destOffset++] = (short)(sampleBuffer[sample] * 32767);
        }
    }
}
