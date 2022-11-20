using SkiaSharp;

namespace TsAudio.WaveForm.Skia;

public record SkiaWaveFormRendererSettings(
    int Width,
    int Height,
    int PixelsPerPeak = 1,
    int SpacerPixels = 0,
    SKPaint TopPeakPen = null,
    SKPaint BottomPeakPen = null);
