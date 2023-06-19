using Microsoft.VisualBasic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TsAudio.Wave.WaveFormats;
using TsAudio.Wave.WaveStreams;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TsAudio.Wav.Wave.WaveProvider;
public class WavWaveStream : WaveStream
{
    private readonly Stream reader;

    public override WaveFormat WaveFormat => throw new NotImplementedException();

    public override long? TotalSamples => throw new NotImplementedException();

    public override long Position => throw new NotImplementedException();

    public override ValueTask InitAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if(buffer.Length % this.WaveFormat.BlockAlign != 0)
        {
            throw new ArgumentException(
                $"Must read complete blocks: requested {buffer.Length}, block align is {WaveFormat.BlockAlign}");
        }

        //var position = this.reader.Position - dataPosition;
        //// sometimes there is more junk at the end of the file past the data chunk
        //if(position + buffer.Length > dataChunkLength)
        //{
        //    buffer = buffer.Slice((int)(dataChunkLength - position));
        //}
        return this.reader.ReadAsync(buffer);
    }

    public override ValueTask SetPositionAsync(long position, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
