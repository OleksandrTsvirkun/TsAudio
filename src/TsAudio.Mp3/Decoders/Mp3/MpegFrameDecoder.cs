using System;
using System.Buffers;
using System.Runtime.InteropServices;

using TsAudio.Utils.Memory;

namespace TsAudio.Decoders.Mp3;

public class MpegFrameDecoder
{
    private LayerIDecoder layerIDecoder;
    private LayerIIDecoder layerIIDecoder;
    private LayerIIIDecoder layerIIIDecoder;

    private float[] eqFactors;

    // channel buffers for getting data out of the decoders...
    // we do it this way so the stereo interleaving code is in one place: DecodeFrameImpl(...)
    // if we ever add support for multi-channel, we'll have to add a pass after the initial
    //  stereo decode (since multi-channel basically uses the stereo channels as a reference)
    private float[] ch0, ch1;

    /// <summary>
    /// Stereo mode used in decoding.
    /// </summary>
    public StereoMode StereoMode { get; set; }

    private MemoryPool<byte> pool;

    public MemoryPool<byte> Pool
    {
        get => this.pool ??= MemoryPool<byte>.Shared;
        set => this.pool = value ?? MemoryPool<byte>.Shared;
    }

    public MpegFrameDecoder()
    {
        this.ch0 = new float[1152];
        this.ch1 = new float[1152];
    }

    /// <summary>
    /// Set the equalizer.
    /// </summary>
    /// <param name="eq">The equalizer, represented by an array of 32 adjustments in dB.</param>
    public void SetEQ(float[] eq)
    {
        if (eq is null)
        {
            this.eqFactors = null;
            return;
        }

        var factors = new float[32];

        for(int i = 0; i < eq.Length; i++)
        {
            // convert from dB -> scaling
            factors[i] = (float)Math.Pow(2, eq[i] / 6);
        }
        this.eqFactors = factors;
    }


    /// <summary>
    /// Decode the Mpeg frame into provided buffer. Do exactly the same as <see cref="DecodeFrame(IMpegFrame, float[], int)"/>
    /// except that the data is written in type as byte array, while still representing single-precision float (in local endian).
    /// </summary>
    /// <param name="frame">The Mpeg frame to be decoded.</param>
    /// <param name="dest">Destination buffer. Decoded PCM (single-precision floating point array) will be written into it.</param>
    /// <param name="destOffset">Writing offset on the destination buffer.</param>
    /// <returns></returns>
    public MemoryOwner<byte> DecodeFrame(IMpegFrame frame)
    {
        if(frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        frame.Reset();

        LayerDecoderBase? curDecoder = this.InitDecoder(frame);

        if(curDecoder is null)
        {
            return default;
        }

        curDecoder.SetEQ(eqFactors);
        curDecoder.StereoMode = StereoMode;

        var cnt = curDecoder.DecodeFrame(frame, this.ch0, this.ch1);

        if(frame.ChannelMode == MpegChannelMode.Mono)
        {
            var sampleCount = cnt * sizeof(float);
            var data = this.Pool.Rent(sampleCount);
            var ch0 = MemoryMarshal.AsBytes(this.ch0.AsSpan(0, cnt));
            ch0.CopyTo(data.Memory.Span);

            return data.Exact(sampleCount);
        }
        else
        {
            var sampleCount = cnt * sizeof(float) * 2;
            var ch0 = MemoryMarshal.AsBytes(this.ch0.AsSpan(0, cnt));
            var ch1 = MemoryMarshal.AsBytes(this.ch1.AsSpan(0, cnt));
            var data = this.Pool.Rent(sampleCount);
            var span = data.Memory.Span;

            for(int i = 0, offset = 0; i < cnt; i++)
            {
                ch0.Slice(i * sizeof(float), sizeof(float)).CopyTo(span.Slice(offset++ * sizeof(float), sizeof(float)));
                ch1.Slice(i * sizeof(float), sizeof(float)).CopyTo(span.Slice(offset++ * sizeof(float), sizeof(float)));
            }
            return data.Exact(sampleCount);
        }
    }


    /// <summary>
    /// Reset the decoder.
    /// </summary>
    public void Reset()
    {
        // the synthesis filters need to be cleared
        this.layerIDecoder?.ResetForSeek();
        this.layerIIDecoder?.ResetForSeek();
        this.layerIIIDecoder?.ResetForSeek();
    }


    private LayerDecoderBase? InitDecoder(IMpegFrame frame)
    {
        return frame.Layer switch
        {
            MpegLayer.LayerI => this.layerIDecoder ??= new LayerIDecoder(),
            MpegLayer.LayerII => this.layerIIDecoder ??= new LayerIIDecoder(),
            MpegLayer.LayerIII => this.layerIIIDecoder ??= new LayerIIIDecoder(),
            _ => null
        };
    }
}
