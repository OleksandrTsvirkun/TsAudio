using System;
using System.Buffers;

using TsAudio.Formats.Mp3;
using TsAudio.Utils.Memory;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders
{
    /// <summary>
    /// Interface for MP3 frame by frame decoder
    /// </summary>
    public interface IMp3FrameDecompressor : IDisposable
    {
        /// <summary>
        /// PCM format that we are converting into
        /// </summary>
        WaveFormat WaveFormat { get; }

        /// <summary>
        /// Decompress a single MP3 frame
        /// </summary>
        /// <param name="frame">Frame to decompress</param>
        /// <returns>Bytes written to output buffer</returns>
        MemoryOwner<byte> DecompressFrame(Mp3Frame frame);

        /// <summary>
        /// Tell the decoder that we have repositioned
        /// </summary>
        void Reset();
    }
}
