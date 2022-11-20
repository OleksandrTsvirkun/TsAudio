namespace TsAudio.Sample.PeekProviders
{
    public struct PeakInfo
    {
        public float Min { get; private set; }

        public float Max { get; private set; }

        public PeakInfo(float min, float max)
        {
            this.Max = max;
            this.Min = min;
        }
    }
}
