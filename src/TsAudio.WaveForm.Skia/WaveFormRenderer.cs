using System;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;
using System.Threading;
using SkiaSharp;
using TsAudio.Wave.WaveForm;
using TsAudio.Utils;

namespace TsAudio.WaveForm.Skia
{
    public class SkiaWaveFormRenderer
    {
        private static TaskScheduler scheduler = new SingleThreadTaskScheduler();

        private CancellationTokenSource cts;

        public SkiaWaveFormRendererSettings Settings { get; }

        public WaveFormRendererData WaveFormRendererData { get; }

        public int X { get; private set; }

        public void Cancel()
        {
            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = null;
        }

        public SkiaWaveFormRenderer(SkiaWaveFormRendererSettings settings, WaveFormRendererData waveFormRendererData)
        {
            this.Settings = settings;
            this.WaveFormRendererData = waveFormRendererData;
        }

        public Task RenderWaveFormAsync(SKCanvas canvas, Action updater = null, CancellationToken cancellationTokenExternal = default)
        {
            this.cts?.Cancel();
            this.cts?.Dispose();
            this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);
            var cancellationToken = this.cts.Token;

            var width = this.Settings.Width;
            var height = this.Settings.Height;
            var metadata = this.WaveFormRendererData.Metadata;
            var waveStream = this.WaveFormRendererData.WaveStream;
            var peakProvider = this.WaveFormRendererData.PeakProvider;

            var sampleProvider = new SampleProvider(waveStream);

            var midPoint = height / 2;

            return Task.Factory.StartNew(async () =>
            {
                try
                {
                    var samplesPerPixel = (int)((metadata.TotalSamples * waveStream.WaveFormat.Channels) / width) * (this.Settings.PixelsPerPeak + this.Settings.SpacerPixels);

                    peakProvider.Init(sampleProvider, samplesPerPixel);

                    var topPen = this.Settings.TopPeakPen;
                    var bottomPen = this.Settings.BottomPeakPen;

                    X = 0;
                    canvas.Clear();

                    while(X < width && await peakProvider.MoveNextAsync())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var currentPeak = peakProvider.Current;
                        var maxLine = height * currentPeak.Max;
                        var minLine = height * currentPeak.Min;

                        for(var n = 0; n < this.Settings.PixelsPerPeak; ++n, ++X)
                        {
                            canvas.DrawLine(X, midPoint, X, midPoint - maxLine, topPen);
                            canvas.DrawLine(X, midPoint, X, midPoint - minLine, bottomPen);
                        }

                        X += this.Settings.SpacerPixels;

                        updater?.Invoke();
                    }
                }
                catch(Exception ex)
                {
                    canvas.Clear(); 
                }
                finally
                {
                    updater?.Invoke();
                    await peakProvider.DisposeAsync();
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, scheduler);
        }
    }
}
