using System.Text.Json;

namespace VisionWorkbench.Core;

public static class ResultExporter
{
    public static void Export(ProcessingResult result, string annotatedPath, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(result);
        var basePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(annotatedPath))!,
            Path.GetFileNameWithoutExtension(annotatedPath));
        File.WriteAllBytes(annotatedPath, result.AnnotatedPng);
        File.WriteAllBytes(basePath + "_mask.png", result.BinaryPng);
        var manifest = new
        {
            SchemaVersion = 2,
            Source = Path.GetFileName(sourceName),
            CreatedAt = DateTimeOffset.Now,
            result.Width,
            result.Height,
            Parameters = result.Parameters,
            Region = result.ProcessingRegion,
            CoordinateSpace = "original-image",
            MaskLayout = "full-size; pixels outside the processing region are zero",
            RoiBoundaryPolicy = "targets intersecting ROI edges are measured as clipped shapes",
            Contours = result.Measurements,
            Units = new { Area = "px^2", Perimeter = "px", Bounds = "px" },
            result.ElapsedMilliseconds,
            TimingScope = "decode + image processing + PNG encoding; excludes file reading and WPF rendering"
        };
        File.WriteAllText(basePath + "_measurements.json", JsonSerializer.Serialize(manifest,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
