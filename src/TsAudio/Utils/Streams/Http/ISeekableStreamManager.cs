using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.Http;

public interface ISeekableStreamManager : IStreamManager
{
    BufferingOptions BufferingOptions { get; }

    Task InitAsync(CancellationToken cancellationToken = default);
}
