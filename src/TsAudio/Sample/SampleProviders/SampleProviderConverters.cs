using System;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveOutputs;
using TsAudio.Wave.WaveProviders;

namespace TsAudio.Sample.SampleProviders
{
    /// <summary>
    /// Utility class for converting to SampleProvider
    /// </summary>
    public static class SampleProviderConverters
    {
        /// <summary>
        /// Helper function to go from IWaveProvider to a SampleProvider
        /// Must already be PCM or IEEE float
        /// </summary>
        /// <param name="waveProvider">The WaveProvider to convert</param>
        /// <returns>A sample provider</returns>
        public static ISampleProvider ConvertWaveProviderIntoSampleProvider(IWaveProvider waveProvider)
        {
            return waveProvider.WaveFormat.Encoding switch {
                WaveFormatEncoding.Pcm => waveProvider.WaveFormat.BitsPerSample switch
                    {
                        8 => new Pcm8BitToSampleProvider(waveProvider),
                        16 => new Pcm16BitToSampleProvider(waveProvider),
                        24 => new Pcm24BitToSampleProvider(waveProvider),
                        32 => new Pcm32BitToSampleProvider(waveProvider),
                        _ => throw new InvalidOperationException("Unsupported bit depth")
                    },
                WaveFormatEncoding.IeeeFloat => waveProvider.WaveFormat.BitsPerSample switch
                    {
                        32 => new WaveToSampleProvider(waveProvider),
                        64 => new WaveToSampleProvider64(waveProvider),
                        _ => throw new InvalidOperationException("Unsupported bit depth")
                    },
                _ => throw new ArgumentException("Unsupported source encoding")
            };
        }
    }
}
