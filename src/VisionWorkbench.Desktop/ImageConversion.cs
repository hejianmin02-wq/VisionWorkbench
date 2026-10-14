using System.IO;
using System.Windows.Media.Imaging;

namespace VisionWorkbench.Desktop;

public static class ImageConversion
{
    public static BitmapSource FromPng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
