using System.Collections.Generic;

using TsAudio.Sample.SampleProviders;

namespace TsAudio.Sample.PeekProviders
{
    public interface IPeakProvider : IAsyncEnumerator<PeakInfo>
    {
        void Init(ISampleProvider reader, int samplesPerPeek);
    }
}
