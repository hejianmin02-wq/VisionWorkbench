using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VisionWorkbench.Core;

namespace VisionWorkbench.Desktop;

internal static class SmokeTest
{
    public static async Task<int> RunAsync(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        using var bindingLog = new StringWriter();
        using var bindingListener = new TextWriterTraceListener(bindingLog);
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingListener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        var checks = new List<string>();
        try
        {
            var window = new MainWindow();
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Opacity = 0;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -10000;
            window.Top = -10000;
            window.WindowStyle = WindowStyle.None;
            window.Width = 1440;
            window.Height = 860;
            window.Show();
            var model = window.ViewModel;
            await model.LoadSampleAsync();
            Require(model.ContourCount == 3, "sample identifies three large shapes", checks);
            Require(model.CanSave, "current result can be exported", checks);
            model.Threshold = 255;
            Require(!model.CanSave, "changed parameters invalidate export", checks);
            await model.ProcessAsync();
            Require(model.ContourCount == 0, "threshold 255 removes all foreground", checks);
            model.Threshold = 128;
            await model.ProcessAsync();
            Require(model.ContourCount == 3, "restoring threshold recovers contours", checks);
            await model.LoadFileAsync(Path.Combine(directory, "does-not-exist.png"));
            Require(model.Status.StartsWith("处理失败") && model.ContourCount == 3,
                "failed load preserves the previous successful result", checks);
            await model.ProcessAsync();
            var result = model.CurrentResult ?? throw new InvalidOperationException("Missing processing result.");
            File.WriteAllBytes(Path.Combine(directory, "sample.png"), result.OriginalPng);
            ResultExporter.Export(result, Path.Combine(directory, "sample_annotated.png"), "synthetic-sample.png");
            Require(File.Exists(Path.Combine(directory, "sample_annotated_measurements.json")),
                "measurement manifest exported", checks);

            var root = (FrameworkElement)window.Content;
            root.Width = 1440;
            root.Height = 860;
            root.Measure(new Size(1440, 860));
            root.Arrange(new Rect(0, 0, 1440, 860));
            root.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var bitmap = new RenderTargetBitmap(1440, 860, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(directory, "workbench.png"))) encoder.Save(stream);

            model.RoiEnabled = true;
            model.RoiXText = "80";
            model.RoiYText = "160";
            model.RoiWidthText = "260";
            model.RoiHeightText = "220";
            Require(!model.CanSave && model.ProcessCommand.CanExecute(null), "ROI changes invalidate export and permit processing", checks);
            await model.ProcessAsync();
            Require(model.ContourCount == 1 && model.CanSave, "ROI detects only the rectangle and can be exported", checks);
            Require(model.Measurements[0].X == 100 && model.Measurements[0].Y == 185,
                "ROI measurement coordinates are restored to the original image", checks);
            Require(model.RoiOverlayVisibility == Visibility.Visible && model.RoiOverlayX == 80 && model.RoiOverlayWidth == 260,
                "valid ROI overlay follows the current parameters", checks);
            var roiResult = model.CurrentResult ?? throw new InvalidOperationException("Missing ROI result.");
            ResultExporter.Export(roiResult, Path.Combine(directory, "roi_annotated.png"), "synthetic-sample.png");
            root.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var roiBitmap = new RenderTargetBitmap(1440, 860, 96, 96, PixelFormats.Pbgra32);
            roiBitmap.Render(root);
            var roiEncoder = new PngBitmapEncoder();
            roiEncoder.Frames.Add(BitmapFrame.Create(roiBitmap));
            using (var roiStream = File.Create(Path.Combine(directory, "workbench-roi.png"))) roiEncoder.Save(roiStream);

            model.RoiWidthText = "not-an-integer";
            Require(!model.CanSave && !model.ProcessCommand.CanExecute(null) && model.RoiValidationMessage.Length > 0,
                "non-integer ROI input disables detection and export", checks);
            Require(model.RoiOverlayVisibility == Visibility.Collapsed, "invalid ROI overlay is hidden without binding errors", checks);
            await model.ProcessAsync();
            Require(ReferenceEquals(model.CurrentResult, roiResult), "invalid ROI processing preserves the last successful result", checks);
            model.RoiWidthText = "260";
            Require(model.CanSave, "restoring ROI parameters restores export eligibility", checks);
            model.RoiXText = "950";
            Require(!model.ProcessCommand.CanExecute(null) && !model.CanSave, "out-of-bounds ROI disables detection and export", checks);
            model.RoiXText = "80";
            await model.LoadFileAsync(Path.Combine(directory, "does-not-exist.png"));
            Require(model.Status.StartsWith("处理失败") && ReferenceEquals(model.CurrentResult, roiResult),
                "failed load preserves the current ROI result", checks);
            model.RoiEnabled = false;
            await model.ProcessAsync();
            Require(model.ContourCount == 3 && model.CurrentResult?.Parameters.Roi is null,
                "disabling ROI recovers whole-image processing", checks);
            model.WholeImageRoiCommand.Execute(null);
            Require(model.RoiEnabled && model.RoiXText == "0" && model.RoiWidthText == "960" && model.RoiHeightText == "600",
                "whole-image ROI command fills the actual image dimensions", checks);
            await model.ProcessAsync();
            Require(model.ContourCount == 3, "whole-image ROI detects all three objects", checks);
            bindingListener.Flush();
            File.WriteAllText(Path.Combine(directory, "binding-errors.txt"), bindingLog.ToString());
            Require(string.IsNullOrWhiteSpace(bindingLog.ToString()), "WPF binding error log is empty", checks);
            File.WriteAllText(Path.Combine(directory, "smoke-test.json"), JsonSerializer.Serialize(new
            {
                Passed = checks.Count, Failed = 0, Checks = checks, Result = result.Measurements,
                RoiParameters = roiResult.Parameters, RoiResult = roiResult.Measurements,
                Rendering = "Actual WPF visual tree rendered offscreen; interactive dialogs require manual verification."
            }, new JsonSerializerOptions { WriteIndented = true }));
            window.Close();
            return 0;
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(directory, "smoke-test-error.txt"), exception.ToString());
            return 1;
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingListener); }
    }

    private static void Require(bool condition, string description, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description);
    }
}
