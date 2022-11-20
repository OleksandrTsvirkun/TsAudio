using System;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Wave.WaveStreams
{
    public interface IWaveStream : IWaveProvider, IDisposable
    {
        /// <summary>
        /// Returns Position in Sample bytes
        /// </summary>
        long Position { get; set; }

        /// <summary>
        /// Returns Length in Sample bytes
        /// </summary>
        long Length { get; }

        TimeSpan TotalTime { get; }

        TimeSpan CurrentTime { get; set; }

        int BlockAlign { get; }
    }
}
