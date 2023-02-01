using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsAudio.Utils.Streams;

public class BufferingOptions
{
    public int PauseWriterThreshold { get; init; }

    public int ResumeWriterThreshold { get; init; }
}
