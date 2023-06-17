using System;
using System.Collections.Generic;

namespace TsAudio.Formats.Mp3;

public interface IMp3IndexStreamReader : IAsyncEnumerable<Mp3Index>, IDisposable, IAsyncDisposable
{
}
