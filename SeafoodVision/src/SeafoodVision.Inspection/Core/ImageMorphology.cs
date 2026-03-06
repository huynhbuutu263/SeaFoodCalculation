using OpenCvSharp;

namespace SeafoodVision.Inspection.Core;

/// <summary>
/// Morphological operations (erode, dilate, open, close) used by inspection rules.
/// </summary>
public static class ImageMorphology
{
    public static Mat Open(Mat src, int kernelSize = 3, int iterations = 1)
    {
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
        var dst = new Mat();
        Cv2.MorphologyEx(src, dst, MorphTypes.Open, kernel, iterations: iterations);
        return dst;
    }

    public static Mat Close(Mat src, int kernelSize = 3, int iterations = 1)
    {
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
        var dst = new Mat();
        Cv2.MorphologyEx(src, dst, MorphTypes.Close, kernel, iterations: iterations);
        return dst;
    }
}

