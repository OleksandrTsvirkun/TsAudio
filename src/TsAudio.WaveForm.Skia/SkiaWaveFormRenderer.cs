using System;
using System.Threading.Tasks;

using TsAudio.Sample.SampleProviders;
using System.Threading;
using SkiaSharp;
using TsAudio.Wave.WaveForm;

namespace TsAudio.WaveForm.Skia;

public class SkiaWaveFormRenderer
{
    private CancellationTokenSource? cts;

    public SkiaWaveFormRendererSettings Settings { get; }

    public WaveFormRendererData WaveFormRendererData { get; }

    private int x;
    public int X => x;

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

    public Task RenderWaveFormAsync(SKCanvas canvas, Action updater, CancellationToken cancellationTokenExternal = default)
    {
        var cancellationToken = this.RenewCancellationToken(cancellationTokenExternal);

        var width = this.Settings.Width;
        var height = this.Settings.Height;
        var metadata = this.WaveFormRendererData.Metadata;
        var waveStream = this.WaveFormRendererData.WaveStream;
        var peakProvider = this.WaveFormRendererData.PeakProvider;
        var spacerPixels = this.Settings.SpacerPixels;
        var topPen = this.Settings.TopPeakPen;
        var bottomPen = this.Settings.BottomPeakPen;
        var pixelsPerPeak = this.Settings.PixelsPerPeak;
        var sampleProvider = new SampleProvider(waveStream);
        var samplesPerPixel = (int)((metadata.TotalSamples * waveStream.WaveFormat.Channels) / width) * (pixelsPerPeak + spacerPixels);

        var midPoint = height / 2;
        peakProvider.Init(sampleProvider, samplesPerPixel);

        x = 0;
        canvas.Clear();

        return Task.Factory.StartNew(async () =>
        {
            try
            {
                while(x < width && await peakProvider.MoveNextAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var currentPeak = peakProvider.Current;
                    var maxLine = midPoint - height * currentPeak.Max;
                    var minLine = midPoint - height * currentPeak.Min;

                    for(var n = 0; n < pixelsPerPeak; ++n, ++x)
                    {
                        canvas.DrawLine(x, midPoint, x, maxLine, topPen);
                        canvas.DrawLine(x, midPoint, x, minLine, bottomPen);
                    }

                    x += spacerPixels;

                    updater();
                }
            }
            catch(OperationCanceledException)
            {

            }
            catch(Exception)
            {

            }
            finally
            {
                updater();
                peakProvider.Dispose();
            }
        }, cancellationToken, TaskCreationOptions.LongRunning | TaskCreationOptions.AttachedToParent, TaskScheduler.Default);
    }

    private CancellationToken RenewCancellationToken(CancellationToken cancellationTokenExternal)
    {
        this.Cancel();
        this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenExternal);
        var cancellationToken = this.cts.Token;
        return cancellationToken;
    }
}
