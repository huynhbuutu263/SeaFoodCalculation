using OpenCvSharp;

namespace SeafoodVision.Inspection.Core;

/// <summary>
/// Threshold helpers to segment objects from background.
/// </summary>
public static class ImageThresholding
{
    public static Mat ToBinary(Mat gray, double threshold = 0, double maxValue = 255)
    {
        var dst = new Mat();
        // Use Otsu by default if threshold == 0
        var threshType = threshold <= 0
            ? ThresholdTypes.Binary | ThresholdTypes.Otsu
            : ThresholdTypes.Binary;

        Cv2.Threshold(gray, dst, threshold, maxValue, threshType);
        return dst;
    }

    public static Mat Adaptive(Mat gray, int blockSize = 11, double c = 2)
    {
        var dst = new Mat();
        Cv2.AdaptiveThreshold(
            gray,
            dst,
            maxValue: 255,
            adaptiveMethod: AdaptiveThresholdTypes.GaussianC,
            thresholdType: ThresholdTypes.Binary,
            blockSize: blockSize,
            c: c);
        return dst;
    }
}

