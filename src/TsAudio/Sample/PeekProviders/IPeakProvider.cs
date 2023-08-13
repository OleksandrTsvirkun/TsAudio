using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;

namespace TsAudio.Sample.PeekProviders; 

public interface IPeakProvider : IAsyncEnumerator<PeakInfo>
{
    void Init(ISampleProvider reader, int samplesPerPeek);
}
