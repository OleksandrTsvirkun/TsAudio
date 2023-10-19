using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsAudio.Decoders.Mp3;

/// <summary>
/// RIFF header reader
/// </summary>
internal class RiffHeaderFrame : FrameBase
{
    internal static RiffHeaderFrame TrySync(uint syncMark)
    {
        if(syncMark == 0x52494646U)
        {
            return new RiffHeaderFrame();
        }

        return null;
    }

    private RiffHeaderFrame()
    {

    }

    protected override int Validate()
    {
        Span<byte> buf = stackalloc byte[4];

        // we expect this to be the "WAVE" chunk
        if(this.Read(8, buf) != 4)
        {
            return -1;
        }

        if(!buf.SequenceEqual(stackalloc byte[4]
        {
            (byte)'W',
            (byte)'A',
            (byte)'V',
            (byte)'E'
        }))

        {
            return -1;
        }

        // now the "fmt " chunk
        if(Read(12, buf) != 4)
        {
            return -1;
        }

        if(!buf.SequenceEqual(stackalloc byte[4]
{
            (byte)'f',
            (byte)'m',
            (byte)'t',
            (byte)' '
        }))
        {
            return -1;
        }

        // we've found the fmt chunk, so look for the data chunk
        var offset = 16;
        while(true)
        {
            // read the length and seek forward
            if(Read(offset, buf) != 4)
            {
                return -1;
            }
               
            offset += 4 + BitConverter.ToInt32(buf);

            // get the chunk ID
            if(Read(offset, buf) != 4)
            {
                return -1;
            }
                
            offset += 4;

            if(!buf.SequenceEqual(stackalloc byte[4]
{
                (byte)'d',
                (byte)'a',
                (byte)'t',
                (byte)'a'
            }))
            {
                break;
            }
        }

        // ... and now we know exactly where the frame ends
        return offset + 4;
    }
}
