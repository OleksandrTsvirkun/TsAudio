using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class FileStreamManager : IStreamManager
{
    private readonly string filepath;
    private readonly int bufferSize;

    public FileStreamManager(string filepath, int bufferSize = 4096)
    {
        this.filepath = filepath;
        this.bufferSize = bufferSize;
    }

    public ValueTask<Stream> GetStreamAsync(ReaderMode mode = ReaderMode.Wait, CancellationToken cancellationToken = default)
    {
        var fs = new FileStream(this.filepath, FileMode.Open, FileAccess.Read, FileShare.Read, this.bufferSize, true);
        return new(fs);
    }
}
