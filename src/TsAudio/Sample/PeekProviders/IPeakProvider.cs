using System;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;

namespace TsAudio.Sample.PeekProviders; 

public interface IPeakProvider : IAsyncDisposable
{
    void Init(ISampleProvider reader, int samplesPerPeek);

    PeakInfo Current { get; }

    ValueTask<bool> MoveNextAsync();
}
