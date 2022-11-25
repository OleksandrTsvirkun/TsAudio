using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TsAudio.Sample.PeekProviders
{
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PeakInfo CalculatePeak(int count)
        {
            var span = this.BufferOwner.Memory.Span.Slice(0, count);

            var left = 0.0f;
            var right = 0.0f;
            var length = span.Length / 2;


            if(Vector.IsHardwareAccelerated)
            {
                var sample = Vector4.Zero;

                for(int i = 0; i < span.Length - 4;)
                {
                    sample += new Vector4(Math.Abs(span[i++]), Math.Abs(span[i++]), Math.Abs(span[i++]), Math.Abs(span[i++]));
                }

                right = this.scale * ((sample.Y + sample.W) / length);
                left = this.scale * ((sample.X + sample.Z) / length);
            }
            else
            {
                for(int i = 0; i < span.Length - 1;)
                {
                    left += Math.Abs(span[i++]);
                    right += Math.Abs(span[i++]);
                }

                right = this.scale * (right / length);
                left = this.scale * (left / length);
            }

            return new PeakInfo(-right, left);
        }
    }
}
