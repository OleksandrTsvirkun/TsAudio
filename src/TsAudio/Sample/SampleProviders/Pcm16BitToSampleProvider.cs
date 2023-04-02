using System;
using System.Runtime.CompilerServices;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders;

/// <summary>
/// Converts an IWaveProvider containing 16 bit PCM to an
/// ISampleProvider
/// </summary>
public class Pcm16BitToSampleProvider : SampleProviderConverterBase<short>
{
    /// <summary>
    /// Initialises a new instance of Pcm16BitToSampleProvider
    /// </summary>
    /// <param name="source">Source wave provider</param>
    public Pcm16BitToSampleProvider(IWaveProvider source) : base(source, 2)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void TransformSample(Span<float> buffer, ReadOnlySpan<short> sourceBuffer, int n, ref int outIndex)
    {
        buffer[outIndex++] = sourceBuffer[n] / 32768f;
    }
}
