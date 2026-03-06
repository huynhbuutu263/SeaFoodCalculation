using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace SeafoodVision.Presentation.Helpers;

public static class MatExtensions
{
    /// <summary>
    /// Converts an OpenCV Mat to a WPF BitmapSource efficiently using memory streams.
    /// This is used heavily for live-previewing inspection pipelines.
    /// </summary>
    public static BitmapSource? ToBitmapSource(this Mat mat)
    {
        if (mat is null || mat.Empty())
            return null;

        try
        {
            // .bmp encoding is fastest for in-memory transfer compared to PNG/JPG
            Cv2.ImEncode(".bmp", mat, out byte[] buffer);

            using var ms = new MemoryStream(buffer);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze(); // Required for cross-thread access

            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
