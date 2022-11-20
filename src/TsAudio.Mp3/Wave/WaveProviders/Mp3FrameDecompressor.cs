using System;
using System.Buffers;

using TsAudio.Decoders.Mp3;
using TsAudio.Formats.Mp3;
using TsAudio.Utils.Memory;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveProviders
{
    public class Mp3FrameDecompressor : IMp3FrameDecompressor
    {
        private readonly MpegFrameDecoder decoder;
        private readonly Mp3FrameWrapper frame;

        public WaveFormat WaveFormat { get; private set; }

        public StereoMode StereoMode
        {
            get => this.decoder.StereoMode;
            set => this.decoder.StereoMode = value;
        }

        public Mp3FrameDecompressor(WaveFormat waveFormat)
        {
            // we assume waveFormat was calculated from the first frame already
            this.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);

            this.decoder = new MpegFrameDecoder();
            this.frame = new Mp3FrameWrapper();
        }

        public MemoryOwner<byte> DecompressFrame(Mp3Frame frame)
        {
            this.frame.WrappedFrame = frame;
            return this.decoder.DecodeFrame(this.frame);
        }

        public void SetEQ(float[] eq)
        {
            this.decoder.SetEQ(eq);
        }

        public void Reset()
        {
            this.decoder.Reset();
        }

        public void Dispose()
        {
            // no-op, since we don't have anything to do here...
        }
    }
}
