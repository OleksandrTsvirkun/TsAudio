using System;

namespace TsAudio.Wave.WaveInputs
{
    /// <summary>
    /// Event Args for WaveInStream event
    /// </summary>
    public class WaveInEventArgs : EventArgs
    {
        /// <summary>
        /// Buffer containing recorded data. Note that it might not be completely
        /// full.
        /// </summary>
        public ReadOnlyMemory<byte> Data { get; private set; }

        /// <summary>
        /// Creates new WaveInEventArgs
        /// </summary>
        public WaveInEventArgs(byte[] buffer, int bytes)
        {
            this.Data = buffer.AsMemory(0, bytes);
        }
    }
}
