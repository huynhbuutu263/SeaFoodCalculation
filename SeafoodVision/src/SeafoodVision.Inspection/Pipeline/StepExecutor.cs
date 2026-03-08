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
        return Execute(src, null, step);
    }

    /// <summary>
    /// Executes a step with an optional secondary input Mat (used by dual-input steps such as
    /// <see cref="StepType.SubtractImage"/> and <see cref="StepType.IntersectionRegion"/>).
    /// </summary>
    public static Mat Execute(Mat src, Mat? secondary, InspectionStep step)
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
            StepType.TemplateMatcher => ApplyTemplateMatcher(src, secondary, step),
            StepType.DefectDetector => ApplyDefectDetector(src, step),
            StepType.CropImage => ApplyCropImage(src, secondary, step),
            StepType.SubtractImage => ApplySubtractImage(src, secondary, step),
            StepType.IntersectionRegion => ApplyIntersectionRegion(src, secondary, step),
            StepType.GetRectangle => ApplyGetRectangle(src, step),
            StepType.AddRegion => ApplyAddRegion(src, step),
            StepType.RegionFeatures => ApplyRegionFeatures(src, step),
            StepType.AffineTransform => ApplyAffineTransform(src, step),
            StepType.SmallestRectangle => ApplySmallestRectangle(src, step),
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
        var kernel = Cv2.GetStructuringElement(p.KernelShape, new OpenCvSharp.Size(p.KernelSize, p.KernelSize));
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

    private static Mat ApplyTemplateMatcher(Mat src, Mat? secondary, InspectionStep step)
    {
        var p = DeserializeParams<TemplateMatcherParams>(step.ParametersJson);

        // Priority: secondary input (from AddRegion step) > drawn region > file path
        Mat? templateMat = null;
        bool ownTemplate = false;

        if (secondary != null && !secondary.Empty())
        {
            templateMat = secondary;
        }
        else if (p.UseDrawnRegion && p.DrawRegionWidth > 0 && p.DrawRegionHeight > 0)
        {
            int dx = Math.Max(0, p.DrawRegionX);
            int dy = Math.Max(0, p.DrawRegionY);
            int dw = Math.Max(1, Math.Min(p.DrawRegionWidth, src.Width - dx));
            int dh = Math.Max(1, Math.Min(p.DrawRegionHeight, src.Height - dy));
            if (dw > 0 && dh > 0)
            {
                templateMat = new Mat(src, new OpenCvSharp.Rect(dx, dy, dw, dh));
                ownTemplate = true;
            }
        }
        else if (!string.IsNullOrEmpty(p.TemplatePath) && System.IO.File.Exists(p.TemplatePath))
        {
            templateMat = Cv2.ImRead(p.TemplatePath, ImreadModes.Color);
            ownTemplate = true;
        }

        if (templateMat == null || templateMat.Empty())
        {
            if (ownTemplate) templateMat?.Dispose();
            return src.Clone();
        }

        try
        {
            if (p.EnableRotation)
            {
                return ApplyRotationAwareTemplateMatcher(src, templateMat, p);
            }

            var result = new Mat();
            Cv2.MatchTemplate(src, templateMat, result, p.Method);

            bool isSqDiff = p.Method == TemplateMatchModes.SqDiff || p.Method == TemplateMatchModes.SqDiffNormed;
            int maxMatches = Math.Max(1, p.MaxMatches);
            double nmsOverlap = Math.Clamp(p.NMSThreshold, 0.0, 1.0);

            var dst = src.Clone();
            // Also produce a binary mask of matched rectangles so downstream steps can use it
            var mask = new Mat(src.Size(), MatType.CV_8UC1, Scalar.Black);
            var accepted = new List<OpenCvSharp.Rect>();

            for (int m = 0; m < maxMatches; m++)
            {
                Cv2.MinMaxLoc(result, out double minVal, out double maxVal,
                    out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

                double score = isSqDiff ? minVal : maxVal;
                bool passed = isSqDiff ? minVal <= p.MatchThreshold : maxVal >= p.MatchThreshold;

                if (!passed) break;

                var matchLoc = isSqDiff ? minLoc : maxLoc;
                var matchRect = new OpenCvSharp.Rect(matchLoc.X, matchLoc.Y, templateMat.Cols, templateMat.Rows);

                // Non-maximum suppression: skip if heavily overlapping a previously accepted match
                bool suppressed = false;
                foreach (var prev in accepted)
                {
                    var intersection = prev & matchRect; // & = intersect for OpenCvSharp Rect
                    double overlapArea = intersection.Width > 0 && intersection.Height > 0
                        ? (double)(intersection.Width * intersection.Height)
                        : 0;
                    double unionArea = prev.Width * prev.Height + matchRect.Width * matchRect.Height - overlapArea;
                    if (unionArea > 0 && overlapArea / unionArea > nmsOverlap)
                    {
                        suppressed = true;
                        break;
                    }
                }

                if (!suppressed)
                {
                    accepted.Add(matchRect);
                    Cv2.Rectangle(dst, matchLoc,
                        new OpenCvSharp.Point(matchLoc.X + templateMat.Cols, matchLoc.Y + templateMat.Rows),
                        Scalar.Red, 2);
                    // Fill the binary mask so downstream GetRectangle / CropImage steps can use it
                    Cv2.Rectangle(mask, matchRect, Scalar.White, Cv2.FILLED);
                }

                // Suppress this peak in the score map so the next iteration finds the next-best match
                int maskPad = Math.Max(1, Math.Min(templateMat.Cols, templateMat.Rows) / 2);
                int mx = Math.Max(0, matchLoc.X - maskPad);
                int my = Math.Max(0, matchLoc.Y - maskPad);
                int mw = Math.Min(result.Cols - mx, templateMat.Cols + maskPad * 2);
                int mh = Math.Min(result.Rows - my, templateMat.Rows + maskPad * 2);
                if (mw > 0 && mh > 0)
                {
                    var maskRegion = new OpenCvSharp.Rect(mx, my, mw, mh);
                    result[maskRegion].SetTo(isSqDiff ? Scalar.All(double.MaxValue) : Scalar.All(-1));
                }
            }

            mask.Dispose();
            result.Dispose();
            return dst;
        }
        finally
        {
            if (ownTemplate) templateMat.Dispose();
        }
    }

    /// <summary>
    /// Rotation-aware template matching: rotates the template over
    /// [<see cref="TemplateMatcherParams.AngleStart"/>, <see cref="TemplateMatcherParams.AngleEnd"/>]
    /// in steps of <see cref="TemplateMatcherParams.AngleStep"/> degrees and keeps the best hit.
    /// An image pyramid (half-resolution coarse scan → full-resolution fine scan) is used to
    /// reduce latency compared to a brute-force full-resolution search.
    /// </summary>
    private static Mat ApplyRotationAwareTemplateMatcher(Mat src, Mat templateMat, TemplateMatcherParams p)
    {
        bool isSqDiff = p.Method == TemplateMatchModes.SqDiff || p.Method == TemplateMatchModes.SqDiffNormed;
        double bestScore = isSqDiff ? double.MaxValue : double.MinValue;
        double bestAngle = 0;
        OpenCvSharp.Point bestLoc = default;
        OpenCvSharp.Size bestSize = new OpenCvSharp.Size(templateMat.Cols, templateMat.Rows);

        double angleStart = Math.Min(p.AngleStart, p.AngleEnd);
        double angleEnd   = Math.Max(p.AngleStart, p.AngleEnd);
        double angleStep  = Math.Max(0.1, Math.Abs(p.AngleStep));

        // ── Coarse pass on half-resolution pyramid ────────────────────────
        using var srcHalf  = new Mat();
        using var tmplHalf = new Mat();
        Cv2.Resize(src, srcHalf,  new OpenCvSharp.Size(src.Cols / 2, src.Rows / 2));
        Cv2.Resize(templateMat, tmplHalf, new OpenCvSharp.Size(templateMat.Cols / 2, templateMat.Rows / 2));

        double coarseStep = Math.Max(angleStep, 5.0);
        double coarseBestAngle = 0;
        double coarseBestScore = isSqDiff ? double.MaxValue : double.MinValue;

        int coarseSteps = (int)Math.Ceiling((angleEnd - angleStart) / coarseStep);
        for (int i = 0; i <= coarseSteps; i++)
        {
            double angle = angleStart + i * coarseStep;
            if (angle > angleEnd) angle = angleEnd;
            using var rotated = RotateImage(tmplHalf, angle);
            if (rotated.Empty() || rotated.Cols > srcHalf.Cols || rotated.Rows > srcHalf.Rows) continue;

            using var resultHalf = new Mat();
            Cv2.MatchTemplate(srcHalf, rotated, resultHalf, p.Method);
            Cv2.MinMaxLoc(resultHalf, out double minV, out double maxV, out _, out _);
            double score = isSqDiff ? minV : maxV;
            bool better  = isSqDiff ? score < coarseBestScore : score > coarseBestScore;
            if (better) { coarseBestScore = score; coarseBestAngle = angle; }
        }

        // ── Fine pass around the coarse winner ────────────────────────────
        double fineStart = Math.Max(angleStart, coarseBestAngle - coarseStep);
        double fineEnd   = Math.Min(angleEnd,   coarseBestAngle + coarseStep);

        int fineSteps = (int)Math.Ceiling((fineEnd - fineStart) / angleStep);
        for (int i = 0; i <= fineSteps; i++)
        {
            double angle = fineStart + i * angleStep;
            if (angle > fineEnd) angle = fineEnd;
            using var rotated = RotateImage(templateMat, angle);
            if (rotated.Empty() || rotated.Cols > src.Cols || rotated.Rows > src.Rows) continue;

            using var resultMat = new Mat();
            Cv2.MatchTemplate(src, rotated, resultMat, p.Method);
            Cv2.MinMaxLoc(resultMat, out double minV, out double maxV,
                out OpenCvSharp.Point minLoc, out OpenCvSharp.Point maxLoc);

            double score = isSqDiff ? minV : maxV;
            bool passed  = isSqDiff ? score <= p.MatchThreshold : score >= p.MatchThreshold;
            bool better  = isSqDiff ? score < bestScore : score > bestScore;
            if (passed && better)
            {
                bestScore = score;
                bestAngle = angle;
                bestLoc   = isSqDiff ? minLoc : maxLoc;
                bestSize  = new OpenCvSharp.Size(rotated.Cols, rotated.Rows);
            }
        }

        var dst = src.Clone();
        if (bestScore != (isSqDiff ? double.MaxValue : double.MinValue))
        {
            // Draw the rotated rectangle on the colour output
            var cx = bestLoc.X + bestSize.Width  / 2.0f;
            var cy = bestLoc.Y + bestSize.Height / 2.0f;
            var rrect = new RotatedRect(new Point2f((float)cx, (float)cy),
                new OpenCvSharp.Size2f(bestSize.Width, bestSize.Height), (float)bestAngle);
            var corners = Cv2.BoxPoints(rrect);
            var pts = corners.Select(pt => new OpenCvSharp.Point((int)pt.X, (int)pt.Y)).ToArray();
            Cv2.Polylines(dst, new[] { pts }, true, Scalar.Red, 2);
        }
        return dst;
    }

    /// <summary>Rotates <paramref name="src"/> by <paramref name="angle"/> degrees around its centre.</summary>
    private static Mat RotateImage(Mat src, double angle)
    {
        var centre = new Point2f(src.Cols / 2.0f, src.Rows / 2.0f);
        using var rot = Cv2.GetRotationMatrix2D(centre, angle, 1.0);

        // Compute new bounding size so the rotated template fits without clipping
        double cos = Math.Abs(rot.At<double>(0, 0));
        double sin = Math.Abs(rot.At<double>(0, 1));
        int newW = (int)(src.Rows * sin + src.Cols * cos);
        int newH = (int)(src.Rows * cos + src.Cols * sin);

        rot.At<double>(0, 2) += (newW - src.Cols) / 2.0;
        rot.At<double>(1, 2) += (newH - src.Rows) / 2.0;

        var dst = new Mat();
        Cv2.WarpAffine(src, dst, rot, new OpenCvSharp.Size(newW, newH),
            flags: InterpolationFlags.Linear,
            borderMode: BorderTypes.Replicate);
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

    // ── Region / Image-manipulation steps ────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="src"/> resized to match <paramref name="target"/>'s dimensions.
    /// When sizes already match the original Mat is returned (no copy); otherwise a new Mat is returned.
    /// </summary>
    private static Mat EnsureSameSize(Mat src, Mat target)
        => src.Size() == target.Size() ? src : src.Resize(target.Size());

    private static Mat ApplyCropImage(Mat src, Mat? secondary, InspectionStep step)
    {
        var p = DeserializeParams<CropParams>(step.ParametersJson);

        OpenCvSharp.Rect roi;

        if (secondary != null && !secondary.Empty() && p.RegionStepOrder > 0)
        {
            // Derive the crop rectangle from the bounding rect of non-zero pixels in the secondary Mat.
            // Works with binary masks (threshold, contour filter) and template crop images.
            Mat? graySecondary = secondary.Channels() == 1
                ? null   // use secondary directly; no conversion needed
                : secondary.CvtColor(ColorConversionCodes.BGR2GRAY);

            Mat grayToUse = graySecondary ?? secondary;
            try
            {
                using var nonZero = new Mat();
                Cv2.FindNonZero(grayToUse, nonZero);

                if (nonZero.Empty())
                    return src.Clone();

                var boundingRect = Cv2.BoundingRect(nonZero);

                // Clamp to source dimensions
                int rx = Math.Max(0, boundingRect.X);
                int ry = Math.Max(0, boundingRect.Y);
                int rw = Math.Min(boundingRect.Width, src.Width - rx);
                int rh = Math.Min(boundingRect.Height, src.Height - ry);
                roi = new OpenCvSharp.Rect(rx, ry, rw, rh);
            }
            finally
            {
                graySecondary?.Dispose();
            }
        }
        else
        {
            int x = Math.Max(0, p.X);
            int y = Math.Max(0, p.Y);
            int w = Math.Max(1, Math.Min(p.Width, src.Width - x));
            int h = Math.Max(1, Math.Min(p.Height, src.Height - y));
            roi = new OpenCvSharp.Rect(x, y, w, h);
        }

        if (roi.Width <= 0 || roi.Height <= 0) return src.Clone();

        return new Mat(src, roi).Clone();
    }

    private static Mat ApplySubtractImage(Mat src, Mat? secondary, InspectionStep step)
    {
        if (secondary is null || secondary.Empty()) return src.Clone();

        using var secResized = EnsureSameSize(secondary, src);
        var diff = new Mat();
        Cv2.Absdiff(src, secResized, diff);
        return diff;
    }

    private static Mat ApplyIntersectionRegion(Mat src, Mat? secondary, InspectionStep step)
    {
        if (secondary is null || secondary.Empty()) return src.Clone();

        // Both inputs must be binary (single-channel). Convert if needed.
        Mat m1 = src.Channels() == 1 ? src : ToGray(src);
        Mat m2 = secondary.Channels() == 1 ? secondary : ToGray(secondary);

        using var m2Sized = EnsureSameSize(m2, m1);
        if (!ReferenceEquals(m2, secondary)) m2.Dispose();

        var dst = new Mat();
        Cv2.BitwiseAnd(m1, m2Sized, dst);

        if (!ReferenceEquals(m1, src)) m1.Dispose();
        return dst;
    }

    private static Mat ApplyGetRectangle(Mat src, InspectionStep step)
    {
        // Convert to binary mask to find contours
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.FindContours(gray, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        // Draw on a colour copy so rectangles are visible
        var dst = src.Channels() == 1
            ? src.CvtColor(ColorConversionCodes.GRAY2BGR)
            : src.Clone();

        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            Cv2.Rectangle(dst, rect, Scalar.LimeGreen, 2);
        }

        if (!ReferenceEquals(gray, src)) gray.Dispose();
        return dst;
    }

    private static Mat ApplyAddRegion(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<AddRegionParams>(step.ParametersJson);

        int tx = Math.Max(0, p.TemplateX);
        int ty = Math.Max(0, p.TemplateY);
        int tw = Math.Max(1, Math.Min(p.TemplateWidth, src.Width - tx));
        int th = Math.Max(1, Math.Min(p.TemplateHeight, src.Height - ty));

        if (tw <= 0 || th <= 0) return src.Clone();

        // Output the cropped template image so that a downstream TemplateMatcher step
        // can reference it via TemplateStepOrder and use it as the template Mat directly.
        return new Mat(src, new OpenCvSharp.Rect(tx, ty, tw, th)).Clone();
    }

    // ── New steps ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts geometric features from the largest contour in a binary mask and
    /// draws them on a colour annotation image:
    /// <list type="bullet">
    ///   <item>Area (px²) and perimeter (px)</item>
    ///   <item>Circularity  4π·A / P²</item>
    ///   <item>Orientation (degrees from horizontal, from fitEllipse)</item>
    ///   <item>Axis-aligned bounding rectangle</item>
    ///   <item>Minimum enclosing circle (outer / circumscribed)</item>
    ///   <item>Largest inscribed circle approximated via distance transform</item>
    /// </list>
    /// </summary>
    private static Mat ApplyRegionFeatures(Mat src, InspectionStep step)
    {
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.FindContours(gray, out var contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        // Work on a colour output image
        var dst = src.Channels() == 1
            ? src.CvtColor(ColorConversionCodes.GRAY2BGR)
            : src.Clone();

        if (contours.Length == 0)
        {
            if (!ReferenceEquals(gray, src)) gray.Dispose();
            return dst;
        }

        // Pick the largest contour by area
        var bestContour = contours
            .OrderByDescending(c => Cv2.ContourArea(c))
            .First();

        double area      = Cv2.ContourArea(bestContour);
        double perimeter = Cv2.ArcLength(bestContour, true);
        double circ      = perimeter > 0 ? 4 * Math.PI * area / (perimeter * perimeter) : 0;

        // Axis-aligned bounding rectangle
        var boundRect = Cv2.BoundingRect(bestContour);
        Cv2.Rectangle(dst, boundRect, new Scalar(0, 255, 0), 2);

        // Minimum enclosing circle (outer / circumscribed circle)
        Cv2.MinEnclosingCircle(bestContour, out Point2f encCenter, out float encRadius);
        Cv2.Circle(dst, new OpenCvSharp.Point((int)encCenter.X, (int)encCenter.Y),
            (int)encRadius, new Scalar(0, 0, 255), 2);

        // Inscribed (inner) circle via distance transform
        using var binary = new Mat();
        var grayForDist = gray.Channels() == 1 ? gray : ToGray(gray);
        Cv2.Threshold(grayForDist, binary, 127, 255, ThresholdTypes.Binary);
        using var dist = new Mat();
        Cv2.DistanceTransform(binary, dist, DistanceTypes.L2, DistanceTransformMasks.Mask5);
        Cv2.MinMaxLoc(dist, out _, out double maxDist, out _, out OpenCvSharp.Point innerCenter);
        if (!ReferenceEquals(grayForDist, gray)) grayForDist.Dispose();
        Cv2.Circle(dst, innerCenter, (int)maxDist, new Scalar(255, 0, 0), 2);

        // Orientation — requires at least 5 points for fitEllipse
        double orientation = 0;
        if (bestContour.Length >= 5)
        {
            var ellipse = Cv2.FitEllipse(bestContour);
            orientation = ellipse.Angle;
            Cv2.Ellipse(dst, ellipse, new Scalar(255, 255, 0), 2);
        }

        // Text overlay: key metrics
        string line1 = $"Area={area:F0} Perim={perimeter:F1} Circ={circ:F3}";
        string line2 = $"Orient={orientation:F1}deg EncR={encRadius:F1} InscR={maxDist:F1}";
        Cv2.PutText(dst, line1, new OpenCvSharp.Point(4, 18),
            HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1, LineTypes.AntiAlias);
        Cv2.PutText(dst, line2, new OpenCvSharp.Point(4, 36),
            HersheyFonts.HersheySimplex, 0.5, Scalar.White, 1, LineTypes.AntiAlias);

        if (!ReferenceEquals(gray, src)) gray.Dispose();
        return dst;
    }

    /// <summary>
    /// Applies a 2-D affine transformation (scale → rotate → translate) to the source image.
    /// </summary>
    private static Mat ApplyAffineTransform(Mat src, InspectionStep step)
    {
        var p = DeserializeParams<AffineTransformParams>(step.ParametersJson);

        double cx = src.Cols / 2.0;
        double cy = src.Rows / 2.0;

        // Build: rotate + scale around the image centre, then apply translation
        using var rot = Cv2.GetRotationMatrix2D(new Point2f((float)cx, (float)cy), p.Angle, 1.0);

        // Apply non-uniform scale: row 0 controls X output (ScaleX), row 1 controls Y output (ScaleY)
        rot.At<double>(0, 0) *= p.ScaleX;
        rot.At<double>(0, 1) *= p.ScaleX;
        rot.At<double>(1, 0) *= p.ScaleY;
        rot.At<double>(1, 1) *= p.ScaleY;

        // Apply translation
        rot.At<double>(0, 2) += p.TranslateX;
        rot.At<double>(1, 2) += p.TranslateY;

        var dst = new Mat();
        Cv2.WarpAffine(src, dst, rot, src.Size(),
            flags: InterpolationFlags.Linear,
            borderMode: BorderTypes.Replicate);
        return dst;
    }

    /// <summary>
    /// Computes the minimum-area rotated bounding rectangle for every contour in a binary
    /// mask and draws it on a colour image using <c>Cv2.BoxPoints</c> + <c>Cv2.Polylines</c>.
    /// </summary>
    private static Mat ApplySmallestRectangle(Mat src, InspectionStep step)
    {
        var gray = src.Channels() == 1 ? src : ToGray(src);
        Cv2.FindContours(gray, out var contours, out _, RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var dst = src.Channels() == 1
            ? src.CvtColor(ColorConversionCodes.GRAY2BGR)
            : src.Clone();

        foreach (var contour in contours)
        {
            var rrect  = Cv2.MinAreaRect(contour);
            var pts    = Cv2.BoxPoints(rrect);
            var iPts   = pts.Select(pt => new OpenCvSharp.Point((int)pt.X, (int)pt.Y)).ToArray();
            Cv2.Polylines(dst, new[] { iPts }, true, Scalar.Cyan, 2);
        }

        if (!ReferenceEquals(gray, src)) gray.Dispose();
        return dst;
    }
}
