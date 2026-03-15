using GlassFactory.BillTracker.App.Models;

namespace GlassFactory.BillTracker.App.Services;

public readonly record struct PrintScaleResult(
    double Scale,
    double TranslateXDip,
    double TranslateYDip,
    double ViewportWidthDip,
    double ViewportHeightDip,
    bool IsFromPrinter,
    string Source);

public static class PrintScaleCalculator
{
    public const double MinScale = 0.5d;
    public const double MaxScale = 1.5d;
    public static readonly double LogicalContentWidthDip = MmToDip(216d);
    public static readonly double LogicalContentHeightDip = MmToDip(93d);

    public static PrintScaleResult Compute(PrintBillOptions options)
    {
        options ??= new PrintBillOptions();

        var hasImageableArea = options.PrinterImageableAreaKnown
            && options.PrinterImageableWidthDip > 0d
            && options.PrinterImageableHeightDip > 0d;

        var scale = 1d;
        var source = "default";
        if (options.FitToPageScale)
        {
            if (hasImageableArea)
            {
                var raw = Math.Min(
                    options.PrinterImageableWidthDip / LogicalContentWidthDip,
                    options.PrinterImageableHeightDip / LogicalContentHeightDip);
                scale = Clamp(raw, MinScale, MaxScale);
                source = options.PrinterImageableAreaFromCapabilities ? "caps-fit" : "fallback-fit";
            }
            else
            {
                scale = 1d;
                source = "default-fit";
            }
        }
        else
        {
            var manualScale = options.ManualScalePercent / 100d;
            scale = Clamp(manualScale, MinScale, MaxScale);
            source = "manual";
        }

        var translateX = 0d;
        var translateY = 0d;
        var viewportWidth = LogicalContentWidthDip;
        var viewportHeight = LogicalContentHeightDip;

        if (hasImageableArea)
        {
            var scaledWidth = LogicalContentWidthDip * scale;
            var scaledHeight = LogicalContentHeightDip * scale;
            var centeredX = Math.Max(0d, (options.PrinterImageableWidthDip - scaledWidth) / 2d);
            var centeredY = Math.Max(0d, (options.PrinterImageableHeightDip - scaledHeight) / 2d);
            translateX = options.PrinterImageableOriginXDip + centeredX;
            translateY = options.PrinterImageableOriginYDip + centeredY;
            viewportWidth = Math.Max(LogicalContentWidthDip, options.PrinterImageableOriginXDip + options.PrinterImageableWidthDip);
            viewportHeight = Math.Max(LogicalContentHeightDip, options.PrinterImageableOriginYDip + options.PrinterImageableHeightDip);
        }

        return new PrintScaleResult(
            Scale: scale,
            TranslateXDip: translateX,
            TranslateYDip: translateY,
            ViewportWidthDip: viewportWidth,
            ViewportHeightDip: viewportHeight,
            IsFromPrinter: hasImageableArea,
            Source: source);
    }

    public static double MmToDip(double mm)
    {
        return mm * 96d / 25.4d;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
