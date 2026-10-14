using OpenCvSharp;

namespace VisionWorkbench.Core;

public static class SampleImages
{
    // Deterministic synthetic input: three large foreground objects and five tiny noise dots.
    public static byte[] CreateShapes()
    {
        using var image = new Mat(600, 960, MatType.CV_8UC3, new Scalar(25, 29, 35));
        for (var x = 0; x < image.Width; x += 40)
            Cv2.Line(image, new Point(x, 0), new Point(x, image.Height - 1), new Scalar(38, 43, 49));
        for (var y = 0; y < image.Height; y += 40)
            Cv2.Line(image, new Point(0, y), new Point(image.Width - 1, y), new Scalar(38, 43, 49));
        Cv2.Rectangle(image, new Rect(100, 185, 220, 170), new Scalar(220, 220, 220), -1);
        Cv2.Circle(image, new Point(480, 275), 85, new Scalar(230, 230, 230), -1);
        Cv2.FillConvexPoly(image, new[] { new Point(730, 175), new Point(850, 355), new Point(640, 355) },
            new Scalar(240, 240, 240));
        for (var index = 0; index < 5; index++)
            Cv2.Circle(image, new Point(160 + index * 135, 460), 2, Scalar.White, -1);
        Cv2.PutText(image, "SYNTHETIC SAMPLE  /  960 x 600", new Point(60, 80),
            HersheyFonts.HersheySimplex, 0.65, new Scalar(96, 96, 96), 1);
        return ImageProcessor.EncodePng(image);
    }
}
