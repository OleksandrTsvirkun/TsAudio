using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams.MemoryMapped;

public class MemoryMappedStreamManager : IStreamManager
{
    private readonly MemoryMappedFile memoryMapped;
    private readonly FileStream fileStream;

    public MemoryMappedStreamManager(FileStream fileStream, bool leaveOpen = false)
    {
        this.fileStream = leaveOpen ? null : fileStream;
        this.memoryMapped = MemoryMappedFile.CreateFromFile(fileStream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.Inheritable, leaveOpen);
    }

    public MemoryMappedStreamManager(string filename)
    {
        this.memoryMapped = MemoryMappedFile.CreateFromFile(filename, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
    }

    public ValueTask<Stream> GetStreamAsync(StreamReadMode mode = StreamReadMode.Wait, CancellationToken cancellationToken = default)
    {
        var stream = this.memoryMapped.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
        return new ValueTask<Stream>(stream);
    }

    protected void DisposeCore()
    {
        this.memoryMapped.Dispose();
    }

    public void Dispose() 
    {
        this.fileStream?.Dispose();
        this.DisposeCore();
    }

    public async ValueTask DisposeAsync()
    {
        if (this.fileStream is not null)
        {
            await this.fileStream.DisposeAsync();
        }
        this.DisposeCore();
    }
}
