using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Sample.PeekProviders;

public class AveragePeakProvider : PeakProvider
{
    private readonly float scale;

    public CancellationToken CancellationToken { get; set; }

    public AveragePeakProvider(float scale = 1f)
    {
        this.scale = scale;
    }

    public override async ValueTask<bool> MoveNextAsync()
    {
        var buffer = this.BufferOwner.Memory.Slice(0, this.SamplesPerPeak);
        var read = await this.Provider.ReadAsync(buffer, this.CancellationToken);

        if (read == 0)
        {
            return false;
        }

        this.Current = this.CalculatePeak(read);

        return true;
    }

    private PeakInfo CalculatePeak(int count)
    {
        var span = this.BufferOwner.Memory.Span.Slice(0, count);

        var left = 0.0f;
        var right = 0.0f;
        var length = span.Length / 2;

        if(Vector.IsHardwareAccelerated)
        {
            this.CalculatePeaksVectorized(span, out left, out right, length);
        }
        else
        {
            this.CalculatePeaksScalared(span, ref left, ref right, length);
        }

        return new PeakInfo(-right, left);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculatePeaksScalared(Span<float> span, ref float left, ref float right, int length)
    {
        for(int i = 0; i < span.Length - 1;)
        {
            left += Math.Abs(span[i++]);
            right += Math.Abs(span[i++]);
        }

        right = this.scale * (right / length);
        left = this.scale * (left / length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculatePeaksVectorized(Span<float> span, out float left, out float right, int length)
    {
        var sample = Vector4.Zero;

        for(int i = 0; i < span.Length - 4;)
        {
            sample += new Vector4(Math.Abs(span[i++]), Math.Abs(span[i++]), Math.Abs(span[i++]), Math.Abs(span[i++]));
        }

        right = this.scale * ((sample.Y + sample.W) / length);
        left = this.scale * ((sample.X + sample.Z) / length);
    }
}
