using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.Enums;
using SeafoodVision.Inspection.Pipeline.Models;

namespace SeafoodVision.Inspection.Pipeline;

/// <summary>
/// Executes a single <see cref="InspectionStep"/> on an OpenCV <see cref="Mat"/>.
/// Deserializes the step's ParametersJson and applies the correct operation.
/// </summary>
public static class StepExecutor
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Mat Execute(Mat src, InspectionStep step)
    {
        return step.StepType switch
        {
            StepType.GrayConvert => ToGray(src),
            StepType.ColorFilter => ApplyColorFilter(src, step),
            StepType.GaussianBlur => ApplyGaussianBlur(src, step),
            StepType.MedianBlur => ApplyMedianBlur(src, step),
            StepType.Threshold => ApplyThreshold(src, step),
            StepType.AdaptiveThreshold => ApplyAdaptiveThreshold(src, step),
            StepType.Morphology => ApplyMorphology(src, step),
            StepType.Canny => ApplyCanny(src, step),
            StepType.ContourFilter => ApplyContourFilter(src, step),
            StepType.BlobDetector => ApplyBlobDetector(src, step),
            StepType.TemplateMatcher => ApplyTemplateMatcher(src, step),
            StepType.DefectDetector => ApplyDefectDetector(src, step),
            _ => throw new NotSupportedException($"Unsupported StepType: {step.StepType}")
        };
    }

    private static T DeserializeParams<T>(string json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }
        catch
        {
            // Log this in production. For now, fall back to safe defaults.
            return new T();
        }
    }

    private static Mat ToGray(Mat src)
    {
        if (src.Channels() == 1) return src.Clone(); // Already gray
        var dst = new Mat();
        Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
        return dst;
    }

    private static Mat ApplyColorFilter(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<ColorFilterParams>(step.ParametersJson);
        var hsv = new Mat();
        Cv2.CvtColor(src, hsv, ColorConversionCodes.BGR2HSV);
        var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(p.HMin, p.SMin, p.VMin), new Scalar(p.HMax, p.SMax, p.VMax), mask);
        hsv.Dispose();
        return mask;
    }

    private static Mat ApplyGaussianBlur(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<GaussianBlurParams>(step.ParametersJson);
        int w = p.KernelWidth % 2 == 0 ? p.KernelWidth + 1 : p.KernelWidth; // must be odd
        int h = p.KernelHeight % 2 == 0 ? p.KernelHeight + 1 : p.KernelHeight;
        var dst = new Mat();
        Cv2.GaussianBlur(src, dst, new OpenCvSharp.Size(w, h), p.SigmaX);
        return dst;
    }

    private static Mat ApplyMedianBlur(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<MedianBlurParams>(step.ParametersJson);
        int k = p.KernelSize % 2 == 0 ? p.KernelSize + 1 : p.KernelSize;
        var dst = new Mat();
        Cv2.MedianBlur(src, dst, k);
        return dst;
    }

    private static Mat ApplyThreshold(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<ThresholdParams>(step.ParametersJson);
        var dst = new Mat();
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.Threshold(gray, dst, p.ThreshValue, p.MaxValue, p.ThreshType);
        if (src.Channels() > 1) gray.Dispose();
        return dst;
    }

    private static Mat ApplyAdaptiveThreshold(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<AdaptiveThresholdParams>(step.ParametersJson);
        int block = p.BlockSize % 2 == 0 ? p.BlockSize + 1 : p.BlockSize;
        if (block < 3) block = 3;
        var dst = new Mat();
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.AdaptiveThreshold(gray, dst, p.MaxValue, p.Method, p.ThreshType, block, p.C);
        if (src.Channels() > 1) gray.Dispose();
        return dst;
    }

    private static Mat ApplyMorphology(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<MorphologyParams>(step.ParametersJson);
        var dst = new Mat();
        var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(p.KernelSize, p.KernelSize));
        Cv2.MorphologyEx(src, dst, p.Operation, kernel, iterations: p.Iterations);
        kernel.Dispose();
        return dst;
    }

    private static Mat ApplyCanny(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<CannyParams>(step.ParametersJson);
        var dst = new Mat();
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.Canny(gray, dst, p.Threshold1, p.Threshold2, p.ApertureSize);
        if (src.Channels() > 1) gray.Dispose();
        return dst;
    }

    private static Mat ApplyContourFilter(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<ContourFilterParams>(step.ParametersJson);
        var gray = src.Channels() == 1 ? src : ToGray(src);
        
        Cv2.FindContours(gray, out var contours, out var hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var mask = new Mat(gray.Size(), MatType.CV_8UC1, Scalar.Black);
        
        for (int i = 0; i < contours.Length; i++)
        {
            var contour = contours[i];
            double area = Cv2.ContourArea(contour);
            if (area < p.MinArea || area > p.MaxArea) continue;
            
            double perimeter = Cv2.ArcLength(contour, true);
            double circularity = perimeter == 0 ? 0 : 4 * Math.PI * area / (perimeter * perimeter);
            if (circularity < p.MinCircularity || circularity > p.MaxCircularity) continue;

            var rect = Cv2.BoundingRect(contour);
            double aspect = rect.Height == 0 ? 0 : (double)rect.Width / rect.Height;
            if (aspect < p.MinAspectRatio || aspect > p.MaxAspectRatio) continue;

            Cv2.DrawContours(mask, contours, i, Scalar.White, Cv2.FILLED);
        }
        
        if (src.Channels() > 1) gray.Dispose();
        return mask;
    }

    private static Mat ApplyBlobDetector(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<BlobDetectorParams>(step.ParametersJson);
        var prms = new SimpleBlobDetector.Params
        {
            MinArea = (float)p.MinArea,
            MaxArea = (float)p.MaxArea,
            FilterByArea = true,
            FilterByCircularity = true,
            MinCircularity = (float)p.MinCircularity,
            FilterByColor = p.FilterByColor,
            BlobColor = (byte)p.BlobColor
        };
        var detector = SimpleBlobDetector.Create(prms);
        
        var gray = src.Channels() == 1 ? src : ToGray(src);
        var keypoints = detector.Detect(gray);
        var dst = new Mat(gray.Size(), MatType.CV_8UC1, Scalar.Black);

        foreach (var kp in keypoints)
        {
            Cv2.Circle(dst, new OpenCvSharp.Point((int)kp.Pt.X, (int)kp.Pt.Y), (int)(kp.Size / 2), Scalar.White, thickness: -1);
        }

        detector.Dispose();
        if (src.Channels() > 1) gray.Dispose();
        return dst;
    }

    private static Mat ApplyTemplateMatcher(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<TemplateMatcherParams>(step.ParametersJson);
        if (string.IsNullOrEmpty(p.TemplatePath) || !System.IO.File.Exists(p.TemplatePath))
            return src.Clone();

        using var template = Cv2.ImRead(p.TemplatePath, ImreadModes.Color);
        if (template.Empty()) return src.Clone();

        var result = new Mat();
        Cv2.MatchTemplate(src, template, result, p.Method);
        
        // Find best match
        Cv2.MinMaxLoc(result, out double minVal, out double maxVal, out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);
        
        // Check if map threshold passes
        bool passed;
        if (p.Method == TemplateMatchModes.SqDiff || p.Method == TemplateMatchModes.SqDiffNormed)
        {
            passed = minVal <= p.MatchThreshold;
        }
        else
        {
            passed = maxVal >= p.MatchThreshold;
        }

        var dst = src.Clone();
        if (passed)
        {
            var matchLoc = (p.Method == TemplateMatchModes.SqDiff || p.Method == TemplateMatchModes.SqDiffNormed) ? minLoc : maxLoc;
            Cv2.Rectangle(dst, matchLoc, new OpenCvSharp.Point(matchLoc.X + template.Cols, matchLoc.Y + template.Rows), Scalar.Red, 2);
        }
        
        return dst;
    }

    private static Mat ApplyDefectDetector(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<DefectDetectorParams>(step.ParametersJson);
        if (string.IsNullOrEmpty(p.ReferencePath) || !System.IO.File.Exists(p.ReferencePath))
            return src.Clone();

        using var reference = Cv2.ImRead(p.ReferencePath, ImreadModes.Color);
        if (reference.Empty() || reference.Size() != src.Size()) 
            return src.Clone();

        var diff = new Mat();
        Cv2.Absdiff(src, reference, diff);
        
        var grayDiff = new Mat();
        Cv2.CvtColor(diff, grayDiff, ColorConversionCodes.BGR2GRAY);
        diff.Dispose();

        var mask = new Mat();
        Cv2.Threshold(grayDiff, mask, p.Sensitivity, 255, ThresholdTypes.Binary);
        grayDiff.Dispose();

        // Optional logic: clean up tiny dots and calculate defect areas via contours like in ApplyContourFilter
        return mask;
    }
}
