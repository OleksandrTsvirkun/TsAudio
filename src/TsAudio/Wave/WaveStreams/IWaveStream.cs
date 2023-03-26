using System;
using System.Threading;
using System.Threading.Tasks;

using TsAudio.Wave.WaveProviders;

namespace TsAudio.Wave.WaveStreams
{
    public interface IWaveStream : IWaveProvider, IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Returns Position in Sample bytes
        /// </summary>
        long Position { get; }

        /// <summary>
        /// Returns Length in Sample bytes
        /// </summary>
        long Length { get; }

        TimeSpan TotalTime { get; }

        TimeSpan CurrentTime { get;  }

        int BlockAlign { get; }

        ValueTask InitAsync(CancellationToken cancellationToken = default);

        ValueTask ChangePositionAsync(long position, CancellationToken cancellationToken = default);

        ValueTask ChangePositionAsync(TimeSpan time, CancellationToken cancellationToken = default);
    }
}
