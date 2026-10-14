using System.Diagnostics;
using OpenCvSharp;

namespace VisionWorkbench.Core;

public sealed class ImageProcessor
{
    public const int MaximumPixels = 16_000_000;
    public const int MaximumFileBytes = 32 * 1024 * 1024;

    public ProcessingResult Process(byte[] encodedImage, ProcessingParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        if (encodedImage.Length is 0 or > MaximumFileBytes)
            throw new ArgumentException("图片为空或超过 32 MB。", nameof(encodedImage));
        cancellationToken.ThrowIfCancellationRequested();
        var timer = Stopwatch.StartNew();
        using var source = Cv2.ImDecode(encodedImage, ImreadModes.Color);
        if (source.Empty())
            throw new InvalidDataException("无法解码图片，请选择有效的 PNG、JPG 或 BMP 文件。");
        if ((long)source.Width * source.Height > MaximumPixels)
            throw new InvalidDataException("图片超过 1600 万像素，请先缩小图片。");
        using var gray = new Mat();
        using var binary = new Mat();
        using var annotated = source.Clone();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        if (parameters.Blur)
            Cv2.GaussianBlur(gray, gray, new Size(3, 3), 0);
        cancellationToken.ThrowIfCancellationRequested();
        Cv2.Threshold(gray, binary, parameters.Threshold, 255,
            parameters.Invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary);
        Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var candidates = contours
            .Select(points => new { Points = points, Area = Cv2.ContourArea(points), Bounds = Cv2.BoundingRect(points) })
            .Where(item => item.Area >= parameters.MinimumArea && item.Area > 0)
            .OrderBy(item => item.Bounds.X).ThenBy(item => item.Bounds.Y).ToArray();
        var measurements = new List<ContourMeasurement>(candidates.Length);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var perimeter = Cv2.ArcLength(candidate.Points, true);
            var circularity = perimeter > 0 ? 4 * Math.PI * candidate.Area / (perimeter * perimeter) : 0;
            var approximation = Cv2.ApproxPolyDP(candidate.Points, 0.02 * perimeter, true);
            var shape = approximation.Length == 3 ? "三角形" :
                approximation.Length == 4 ? "四边形" : circularity >= 0.8 ? "近圆形" : "不规则";
            var bounds = candidate.Bounds;
            var id = measurements.Count + 1;
            measurements.Add(new(id, shape, candidate.Area, perimeter, circularity,
                bounds.X, bounds.Y, bounds.Width, bounds.Height));
            Cv2.DrawContours(annotated, new[] { candidate.Points }, -1, new Scalar(76, 194, 41), 3);
            Cv2.Rectangle(annotated, bounds, new Scalar(235, 178, 24), 2);
            Cv2.PutText(annotated, $"#{id}", new Point(bounds.X, Math.Max(20, bounds.Y - 12)),
                HersheyFonts.HersheySimplex, 0.75, new Scalar(235, 220, 120), 2);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var originalPng = EncodePng(source);
        var binaryPng = EncodePng(binary);
        var annotatedPng = EncodePng(annotated);
        timer.Stop();
        return new(originalPng, binaryPng, annotatedPng, measurements.AsReadOnly(), source.Width,
            source.Height, timer.Elapsed.TotalMilliseconds, parameters);
    }

    public static byte[] EncodePng(Mat image)
    {
        if (!Cv2.ImEncode(".png", image, out var bytes))
            throw new IOException("图片编码失败。");
        return bytes;
    }
}
