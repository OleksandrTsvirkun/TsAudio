using System;
using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Helper class turning an already 64 bit floating point IWaveProvider
/// into an ISampleProvider - hopefully not needed for most applications
/// </summary>
public class WaveToSampleProvider64 : SampleProviderConverterBase<long>
{
    /// <summary>
    /// Initializes a new instance of the WaveToSampleProvider class
    /// </summary>
    /// <param name="source">Source wave provider, must be IEEE float</param>
    public WaveToSampleProvider64(IWaveProvider source) : base(source, 8)
    {
        if(source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new ArgumentException("Must be already floating point");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void TransformSample(Span<float> buffer, ReadOnlySpan<long> sourceBuffer, int n, ref int outIndex)
    {
        buffer[outIndex++] = (float)BitConverter.Int64BitsToDouble(sourceBuffer[outIndex]);
    }
}
