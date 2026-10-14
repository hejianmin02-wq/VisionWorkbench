using System.Text.Json;
using OpenCvSharp;
using VisionWorkbench.Core;
using Xunit;

namespace VisionWorkbench.Tests;

public sealed class RoiTests
{
    private readonly ImageProcessor _processor = new();

    [Fact]
    public void WholeImageRoiPreservesMeasurementsMaskAndOriginal()
    {
        var input = SampleImages.CreateShapes();
        var whole = _processor.Process(input, new(Blur: true));
        var roi = _processor.Process(input, new(Blur: true, Roi: new(0, 0, 960, 600)));
        Assert.Equal(whole.Measurements, roi.Measurements);
        Assert.Equal(whole.BinaryPng, roi.BinaryPng);
        Assert.Equal(whole.OriginalPng, roi.OriginalPng);
        Assert.Equal(new ImageRegion(0, 0, 960, 600), roi.ProcessingRegion);
    }

    [Fact]
    public void RoiSelectsOneObjectAndRestoresOriginalCoordinates()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new(Roi: new(80, 160, 260, 220)));
        var item = Assert.Single(result.Measurements);
        Assert.Equal((100, 185, 220, 170), (item.X, item.Y, item.Width, item.Height));
        Assert.Equal(37011, item.Area);
        Assert.Equal(1, item.Id);
        Assert.Equal((960, 600), (result.Width, result.Height));
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal((960, 600), (mask.Width, mask.Height));
        Assert.Equal(255, mask.At<byte>(200, 120));
        Assert.Equal(0, mask.At<byte>(275, 480));
        Assert.Equal(0, mask.At<byte>(159, 120));
        using var marked = Cv2.ImDecode(result.AnnotatedPng, ImreadModes.Color);
        // The blue bounding box is drawn after the green contour, at original x=100 rather than local x=20.
        Assert.Equal(new Vec3b(235, 178, 24), marked.At<Vec3b>(250, 100));
        using var original = Cv2.ImDecode(result.OriginalPng, ImreadModes.Color);
        Assert.Equal(original.At<Vec3b>(250, 20), marked.At<Vec3b>(250, 20));
    }

    [Fact]
    public void RoiWithNoForegroundReturnsAnEmptyFullSizeMask()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new(Roi: new(0, 0, 20, 20)));
        Assert.Empty(result.Measurements);
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal(0, Cv2.CountNonZero(mask));
    }

    [Fact]
    public void TargetCrossingRoiEdgeIsMeasuredAsAClippedShape()
    {
        using var source = new Mat(80, 90, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(source, new Rect(10, 10, 30, 30), Scalar.White, -1);
        var result = _processor.Process(ImageProcessor.EncodePng(source), new(MinimumArea: 1, Roi: new(20, 20, 40, 40)));
        var item = Assert.Single(result.Measurements);
        Assert.Equal((20, 20, 20, 20), (item.X, item.Y, item.Width, item.Height));
        Assert.Equal(361, item.Area);
    }

    [Fact]
    public void InvertedRoiDoesNotTurnOutsidePixelsIntoForeground()
    {
        using var source = new Mat(80, 90, MatType.CV_8UC3, Scalar.White);
        Cv2.Rectangle(source, new Rect(30, 30, 20, 20), Scalar.Black, -1);
        var result = _processor.Process(ImageProcessor.EncodePng(source), new(MinimumArea: 1, Invert: true, Roi: new(20, 20, 40, 40)));
        var item = Assert.Single(result.Measurements);
        Assert.Equal((30, 30, 20, 20), (item.X, item.Y, item.Width, item.Height));
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal(0, mask.At<byte>(0, 0));
        Assert.Equal(255, mask.At<byte>(35, 35));
    }

    [Fact]
    public void BlurUsesOnlyPixelsInsideRoi()
    {
        using var source = new Mat(50, 50, MatType.CV_8UC3, Scalar.White);
        Cv2.Rectangle(source, new Rect(10, 10, 20, 20), Scalar.Black, -1);
        var result = _processor.Process(ImageProcessor.EncodePng(source), new(Threshold: 10, MinimumArea: 0, Blur: true, Roi: new(10, 10, 20, 20)));
        Assert.Empty(result.Measurements);
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal(0, Cv2.CountNonZero(mask));
    }

    [Fact]
    public void RoiCanTouchRightAndBottomImageEdges()
    {
        using var source = new Mat(50, 50, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(source, new Rect(40, 40, 10, 10), Scalar.White, -1);
        var result = _processor.Process(ImageProcessor.EncodePng(source), new(MinimumArea: 1, Roi: new(40, 40, 10, 10)));
        var item = Assert.Single(result.Measurements);
        Assert.Equal((40, 40, 10, 10), (item.X, item.Y, item.Width, item.Height));
    }

    [Fact]
    public void OnePixelRoiIsValidButHasNoPositiveAreaContour()
    {
        using var source = new Mat(10, 10, MatType.CV_8UC3, Scalar.White);
        var result = _processor.Process(ImageProcessor.EncodePng(source), new(MinimumArea: 0, Roi: new(9, 9, 1, 1)));
        Assert.Empty(result.Measurements);
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal(1, Cv2.CountNonZero(mask));
    }

    [Theory]
    [InlineData(-1, 0, 10, 10)]
    [InlineData(0, -1, 10, 10)]
    [InlineData(0, 0, 0, 10)]
    [InlineData(0, 0, 10, 0)]
    [InlineData(0, 0, -1, 10)]
    [InlineData(0, 0, 10, -1)]
    [InlineData(960, 0, 1, 1)]
    [InlineData(0, 600, 1, 1)]
    [InlineData(950, 0, 11, 10)]
    [InlineData(0, 590, 10, 11)]
    [InlineData(int.MaxValue, 0, int.MaxValue, 1)]
    [InlineData(0, int.MaxValue, 1, int.MaxValue)]
    public void InvalidAndOverflowingRoisAreRejected(int x, int y, int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _processor.Process(SampleImages.CreateShapes(), new(Roi: new(x, y, width, height))));
    }

    [Fact]
    public void ExportRecordsRoiAndOriginalCoordinateSpace()
    {
        var region = new ImageRegion(80, 160, 260, 220);
        var result = _processor.Process(SampleImages.CreateShapes(), new(Roi: region));
        var directory = Path.Combine(Path.GetTempPath(), "vision-workbench-roi-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            ResultExporter.Export(result, Path.Combine(directory, "result.png"), "sample.png");
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "result_measurements.json")));
            var root = manifest.RootElement;
            Assert.Equal(2, root.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal("original-image", root.GetProperty("CoordinateSpace").GetString());
            Assert.Equal(80, root.GetProperty("Parameters").GetProperty("Roi").GetProperty("X").GetInt32());
            Assert.Equal(220, root.GetProperty("Region").GetProperty("Height").GetInt32());
            Assert.Equal(100, root.GetProperty("Contours")[0].GetProperty("X").GetInt32());
            Assert.Equal(result.BinaryPng, File.ReadAllBytes(Path.Combine(directory, "result_mask.png")));
        }
        finally
        {
            foreach (var name in new[] { "result.png", "result_mask.png", "result_measurements.json" }) File.Delete(Path.Combine(directory, name));
            Directory.Delete(directory);
        }
    }
}
