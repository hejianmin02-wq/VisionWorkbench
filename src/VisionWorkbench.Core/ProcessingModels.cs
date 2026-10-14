namespace VisionWorkbench.Core;

public sealed record ProcessingParameters(int Threshold = 128, double MinimumArea = 500,
    bool Invert = false, bool Blur = false)
{
    public void Validate()
    {
        if (Threshold is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(Threshold), "阈值应为 0—255。");
        if (!double.IsFinite(MinimumArea) || MinimumArea < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumArea), "最小面积应为非负有限数值。");
    }
}

public sealed record ContourMeasurement(int Id, string Shape, double Area, double Perimeter,
    double Circularity, int X, int Y, int Width, int Height);

// No native Mat crosses the processing boundary; all returned images are encoded byte arrays.
public sealed record ProcessingResult(byte[] OriginalPng, byte[] BinaryPng, byte[] AnnotatedPng,
    IReadOnlyList<ContourMeasurement> Measurements, int Width, int Height,
    double ElapsedMilliseconds, ProcessingParameters Parameters);
