using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public interface IStreamManager
{
    ValueTask<Stream> GetStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default);
}
