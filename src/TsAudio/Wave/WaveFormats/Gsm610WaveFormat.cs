﻿using System;
using System.Runtime.InteropServices;
using System.IO;
using TsAudio.Wave.WaveFormats;

namespace TsAudio.Wave.WaveFormats;

/// <summary>
/// GSM 610
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public class Gsm610WaveFormat : WaveFormat
{
    private readonly short samplesPerBlock;

    /// <summary>
    /// Creates a GSM 610 WaveFormat
    /// For now hardcoded to 13kbps
    /// </summary>
    public Gsm610WaveFormat()
    {
        this.waveFormatTag = WaveFormatEncoding.Gsm610;
        this.channels = 1;
        this.averageBytesPerSecond = 1625;
        this.bitsPerSample = 0; 
        this.blockAlign = 65;
        this.sampleRate = 8000;
        this.extraSize = 2;
        this.samplesPerBlock = 320;
    }

    /// <summary>
    /// Samples per block
    /// </summary>
    public short SamplesPerBlock => this.samplesPerBlock;
}
