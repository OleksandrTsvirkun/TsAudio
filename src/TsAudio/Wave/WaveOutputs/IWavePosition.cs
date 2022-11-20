using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveOutputs
{
    /// <summary>
    /// Interface for IWavePlayers that can report position
    /// </summary>
    public interface IWavePosition
    {

        /// <summary>
        /// Gets a <see cref="Wave.WaveFormat"/> instance indicating the format the hardware is using.
        /// </summary>
        WaveFormat WaveFormat { get; }

        /// <summary>
        /// Position (in terms of bytes played - does not necessarily translate directly to the position within the source audio file)
        /// </summary>
        /// <returns>Position in bytes</returns>
        long Position { get; }
    }
}
