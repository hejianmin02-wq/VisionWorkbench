namespace VisionWorkbench.Core;

/// <summary>A rectangular region in original-image pixels, with an exclusive right/bottom edge.</summary>
public sealed record ImageRegion(int X, int Y, int Width, int Height)
{
    public void Validate()
    {
        if (X < 0 || Y < 0 || Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(ImageRegion), "ROI 的 X、Y 应为非负整数，宽、高应为正整数。");
    }

    public void ValidateWithin(int imageWidth, int imageHeight)
    {
        Validate();
        if ((long)X + Width > imageWidth || (long)Y + Height > imageHeight)
            throw new ArgumentOutOfRangeException(nameof(ImageRegion), "ROI 超出图片边界，请调整区域或关闭 ROI 后重新加载图片。");
    }
}

public sealed record ProcessingParameters(int Threshold = 128, double MinimumArea = 500,
    bool Invert = false, bool Blur = false, ImageRegion? Roi = null)
{
    public void Validate()
    {
        if (Threshold is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(Threshold), "阈值应为 0—255。");
        if (!double.IsFinite(MinimumArea) || MinimumArea < 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumArea), "最小面积应为非负有限数值。");
        Roi?.Validate();
    }
}

public sealed record ContourMeasurement(int Id, string Shape, double Area, double Perimeter,
    double Circularity, int X, int Y, int Width, int Height);

// No native Mat crosses the processing boundary; all returned images are encoded byte arrays.
public sealed record ProcessingResult(byte[] OriginalPng, byte[] BinaryPng, byte[] AnnotatedPng,
    IReadOnlyList<ContourMeasurement> Measurements, int Width, int Height,
    double ElapsedMilliseconds, ProcessingParameters Parameters)
{
    public ImageRegion ProcessingRegion => Parameters.Roi ?? new(0, 0, Width, Height);
}
