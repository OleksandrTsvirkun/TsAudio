using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Sample.PeekProviders;

public class AveragePeakProvider : PeakProvider
{
    private readonly float scale;

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

        var memory = this.BufferOwner.Memory.Slice(0, read);

        this.Current = this.CalculatePeak(memory.Span);

        return true;
    }

    private PeakInfo CalculatePeak(ReadOnlySpan<float> span)
    {
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
    private void CalculatePeaksScalared(ReadOnlySpan<float> span, ref float left, ref float right, int length)
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
    private void CalculatePeaksVectorized(ReadOnlySpan<float> span, out float left, out float right, int length)
    {
        var sample = Vector4.Zero;

        for(int i = 0; i < span.Length - 4; i += 4)
        {
            var vector = new Vector4(span.Slice(i, 4));
            sample += Vector4.Abs(vector);
        }

        right = this.scale * ((sample.Y + sample.W) / length);
        left = this.scale * ((sample.X + sample.Z) / length);
    }
}
