using System.Text.Json;
using OpenCvSharp;
using VisionWorkbench.Core;
using Xunit;

namespace VisionWorkbench.Tests;

public sealed class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    [Fact]
    public void SyntheticSampleHasThreeObjectsInLeftToRightOrder()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new());
        Assert.Equal(3, result.Measurements.Count);
        Assert.Equal(new[] { "四边形", "近圆形", "三角形" }, result.Measurements.Select(item => item.Shape));
        Assert.Equal(new[] { 1, 2, 3 }, result.Measurements.Select(item => item.Id));
        Assert.Equal(960, result.Width);
        Assert.Equal(600, result.Height);
        Assert.All(result.Measurements, item => Assert.True(item.Area > 500));
    }

    [Fact]
    public void MinimumAreaUsesContourAreaRatherThanBoundingBoxArea()
    {
        using var image = new Mat(50, 50, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(image, new Rect(10, 10, 16, 16), Scalar.White, -1);
        var encoded = ImageProcessor.EncodePng(image);
        var retained = _processor.Process(encoded, new(MinimumArea: 225));
        var measurement = Assert.Single(retained.Measurements);
        Assert.Equal(225, measurement.Area);
        Assert.Equal(16, measurement.Width);
        Assert.Equal(16, measurement.Height);
        Assert.Empty(_processor.Process(encoded, new(MinimumArea: 226)).Measurements);
    }

    [Fact]
    public void BinaryThresholdIsStrictlyGreaterThanTheThreshold()
    {
        using var image = new Mat(60, 60, MatType.CV_8UC3, Scalar.Black);
        Cv2.Rectangle(image, new Rect(10, 10, 30, 30), new Scalar(128, 128, 128), -1);
        var encoded = ImageProcessor.EncodePng(image);
        Assert.Empty(_processor.Process(encoded, new(128, 1)).Measurements);
        Assert.Single(_processor.Process(encoded, new(127, 1)).Measurements);
    }

    [Fact]
    public void InvertedModeFindsDarkTargetOnWhiteBackground()
    {
        using var image = new Mat(80, 80, MatType.CV_8UC3, Scalar.White);
        Cv2.Rectangle(image, new Rect(20, 25, 30, 20), Scalar.Black, -1);
        var result = _processor.Process(ImageProcessor.EncodePng(image), new(128, 1, Invert: true));
        var item = Assert.Single(result.Measurements);
        Assert.Equal((20, 25, 30, 20), (item.X, item.Y, item.Width, item.Height));
    }

    [Fact]
    public void BlankImageHasNoContoursEvenWithZeroMinimumArea()
    {
        using var image = new Mat(40, 40, MatType.CV_8UC3, Scalar.Black);
        Assert.Empty(_processor.Process(ImageProcessor.EncodePng(image), new(MinimumArea: 0)).Measurements);
    }

    [Fact]
    public void OptionalBlurStillFindsTheLargeSampleObjects()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new(Blur: true));
        Assert.Equal(3, result.Measurements.Count);
    }

    [Fact]
    public void ResultContainsDecodableImagesAndABinaryMask()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new());
        foreach (var bytes in new[] { result.OriginalPng, result.BinaryPng, result.AnnotatedPng })
        {
            using var image = Cv2.ImDecode(bytes, ImreadModes.Color);
            Assert.False(image.Empty());
            Assert.Equal(960, image.Width);
            Assert.Equal(600, image.Height);
        }
        using var mask = Cv2.ImDecode(result.BinaryPng, ImreadModes.Grayscale);
        Assert.Equal(0, mask.At<byte>(0, 0));
        Assert.Equal(255, mask.At<byte>(200, 120));
        Assert.NotEqual(result.OriginalPng, result.AnnotatedPng);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void RejectsOutOfRangeThreshold(int threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _processor.Process(SampleImages.CreateShapes(), new(threshold)));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidMinimumArea(double minimumArea)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _processor.Process(SampleImages.CreateShapes(), new(MinimumArea: minimumArea)));
    }

    [Fact]
    public void RejectsInvalidEncodedImage()
    {
        Assert.Throws<InvalidDataException>(() => _processor.Process(new byte[] { 1, 2, 3, 4 }, new()));
    }

    [Fact]
    public void HonorsAnAlreadyCanceledToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => _processor.Process(SampleImages.CreateShapes(), new(), cancellation.Token));
    }

    [Fact]
    public void ExportIncludesResultParametersAndPixelUnits()
    {
        var result = _processor.Process(SampleImages.CreateShapes(), new(150, 1000));
        var directory = Path.Combine(Path.GetTempPath(), "vision-workbench-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            ResultExporter.Export(result, Path.Combine(directory, "result.png"), "sample.png");
            Assert.Equal(result.AnnotatedPng, File.ReadAllBytes(Path.Combine(directory, "result.png")));
            Assert.Equal(result.BinaryPng, File.ReadAllBytes(Path.Combine(directory, "result_mask.png")));
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "result_measurements.json")));
            Assert.Equal(150, manifest.RootElement.GetProperty("Parameters").GetProperty("Threshold").GetInt32());
            Assert.Equal("px^2", manifest.RootElement.GetProperty("Units").GetProperty("Area").GetString());
            Assert.Equal(3, manifest.RootElement.GetProperty("Contours").GetArrayLength());
        }
        finally
        {
            foreach (var fileName in new[] { "result.png", "result_mask.png", "result_measurements.json" })
                File.Delete(Path.Combine(directory, fileName));
            Directory.Delete(directory);
        }
    }
}
