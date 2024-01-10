using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Helper class turning an already 32 bit floating point IWaveProvider
/// into an ISampleProvider - hopefully not needed for most applications
/// </summary>
public class WaveToSampleProvider : SampleProviderConverterBase<byte>
{
    /// <summary>
    /// Initializes a new instance of the WaveToSampleProvider class
    /// </summary>
    /// <param name="source">Source wave provider, must be IEEE float</param>
    public WaveToSampleProvider(IWaveProvider source) : base(source, 4)
    {
        if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new ArgumentException("Must be already floating point");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void TransformSample(Span<float> buffer, ReadOnlySpan<byte> sourceBuffer, int n, ref int outIndex)
    {

    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void TransformSamples(Span<float> buffer, int size, ReadOnlySpan<byte> sourceBuffer)
    {
        MemoryMarshal.Cast<byte, float>(sourceBuffer).CopyTo(buffer);
    }
}
