namespace TsAudio.Decoders.Mp3
{
    internal class VBRInfo
    {
        internal VBRInfo() { }

        internal int SampleCount { get; set; }
        internal int SampleRate { get; set; }
        internal int Channels { get; set; }
        internal int VBRFrames { get; set; }
        internal int VBRBytes { get; set; }
        internal int VBRQuality { get; set; }
        internal int VBRDelay { get; set; }

        internal long VBRStreamSampleCount => this.VBRFrames * this.SampleCount;

        internal int VBRAverageBitrate => (int)(this.VBRBytes / (this.VBRStreamSampleCount / (double)this.SampleRate) * 8);
    }
}
