using OpenCvSharp;

namespace SeafoodVision.Inspection.Core;

/// <summary>
/// Simple template matching utilities for visual inspection.
/// </summary>
public static class TemplateMatcher
{
    public sealed record MatchResult(Rect Region, double Score, double Angle = 0);

    /// <summary>
    /// Finds template matches above the specified score threshold.
    /// </summary>
    public static IReadOnlyList<MatchResult> FindMatches(
        Mat source,
        Mat template,
        double minScore = 0.8,
        TemplateMatchModes mode = TemplateMatchModes.CCoeffNormed)
    {
        using var result = new Mat();
        Cv2.MatchTemplate(source, template, result, mode);
        Cv2.MinMaxLoc(result, out _, out var maxVal, out _, out var maxLoc);

        var matches = new List<MatchResult>();
        if (maxVal >= minScore)
        {
            var region = new Rect(maxLoc.X, maxLoc.Y, template.Width, template.Height);
            matches.Add(new MatchResult(region, maxVal));
        }

        return matches;
    }

    /// <summary>
    /// Rotation-invariant template matching: rotates <paramref name="template"/> over
    /// [<paramref name="angleStart"/>, <paramref name="angleEnd"/>] in steps of
    /// <paramref name="angleStep"/> degrees and returns the best match across all tested
    /// angles.  A two-stage pyramid strategy (coarse half-resolution scan followed by a
    /// fine full-resolution scan around the coarse winner) significantly reduces latency
    /// compared to a brute-force full-resolution search over every angle.
    /// </summary>
    /// <param name="source">Source image to search in.</param>
    /// <param name="template">Template image to search for.</param>
    /// <param name="angleStart">Start of the angle search range (degrees).</param>
    /// <param name="angleEnd">End of the angle search range (degrees).</param>
    /// <param name="angleStep">Angular resolution of the fine scan (degrees).</param>
    /// <param name="minScore">Minimum normalised score to accept a match.</param>
    /// <param name="mode">OpenCV template-matching mode (normalised modes recommended).</param>
    /// <returns>A (possibly empty) list of matches sorted by descending score.</returns>
    public static IReadOnlyList<MatchResult> FindMatchesRotationInvariant(
        Mat source,
        Mat template,
        double angleStart = -30,
        double angleEnd = 30,
        double angleStep = 5,
        double minScore = 0.8,
        TemplateMatchModes mode = TemplateMatchModes.CCoeffNormed)
    {
        bool isSqDiff = mode == TemplateMatchModes.SqDiff || mode == TemplateMatchModes.SqDiffNormed;

        // ── Coarse pass (half resolution, large angle step) ──────────────
        using var srcHalf  = new Mat();
        using var tmplHalf = new Mat();
        Cv2.Resize(source,   srcHalf,  new Size(source.Cols   / 2, source.Rows   / 2));
        Cv2.Resize(template, tmplHalf, new Size(template.Cols / 2, template.Rows / 2));

        double coarseStep = Math.Max(Math.Abs(angleStep), 5.0);
        double coarseBestAngle = angleStart;
        double coarseBestScore = isSqDiff ? double.MaxValue : double.MinValue;

        double aStart = Math.Min(angleStart, angleEnd);
        double aEnd   = Math.Max(angleStart, angleEnd);
        double aStep  = Math.Max(0.1, Math.Abs(angleStep));

        int coarseSteps = (int)Math.Ceiling((aEnd - aStart) / coarseStep);
        for (int i = 0; i <= coarseSteps; i++)
        {
            double a = aStart + i * coarseStep;
            if (a > aEnd) a = aEnd;
            using var rotated = RotateImage(tmplHalf, a);
            if (rotated.Empty() || rotated.Cols > srcHalf.Cols || rotated.Rows > srcHalf.Rows) continue;

            using var res = new Mat();
            Cv2.MatchTemplate(srcHalf, rotated, res, mode);
            Cv2.MinMaxLoc(res, out double minV, out double maxV, out _, out _);
            double score = isSqDiff ? minV : maxV;
            if (isSqDiff ? score < coarseBestScore : score > coarseBestScore)
            {
                coarseBestScore = score;
                coarseBestAngle = a;
            }
        }

        // ── Fine pass (full resolution, narrow window) ───────────────────
        double fineStart = Math.Max(aStart, coarseBestAngle - coarseStep);
        double fineEnd   = Math.Min(aEnd,   coarseBestAngle + coarseStep);

        var matches = new List<MatchResult>();

        int fineSteps = (int)Math.Ceiling((fineEnd - fineStart) / aStep);
        for (int i = 0; i <= fineSteps; i++)
        {
            double a = fineStart + i * aStep;
            if (a > fineEnd) a = fineEnd;
            using var rotated = RotateImage(template, a);
            if (rotated.Empty() || rotated.Cols > source.Cols || rotated.Rows > source.Rows) continue;

            using var res = new Mat();
            Cv2.MatchTemplate(source, rotated, res, mode);
            Cv2.MinMaxLoc(res, out double minV, out double maxV,
                out Point minLoc, out Point maxLoc);

            double score  = isSqDiff ? minV : maxV;
            bool   passed = isSqDiff ? score <= minScore : score >= minScore;
            if (!passed) continue;

            var loc    = isSqDiff ? minLoc : maxLoc;
            var region = new Rect(loc.X, loc.Y, rotated.Cols, rotated.Rows);
            matches.Add(new MatchResult(region, score, a));
        }

        return isSqDiff
            ? matches.OrderBy(m => m.Score).ToList()
            : matches.OrderByDescending(m => m.Score).ToList();
    }

    /// <summary>Rotates <paramref name="src"/> by <paramref name="angle"/> degrees around its centre,
    /// expanding the canvas so no pixels are clipped.</summary>
    private static Mat RotateImage(Mat src, double angle)
    {
        var centre = new Point2f(src.Cols / 2.0f, src.Rows / 2.0f);
        using var rot = Cv2.GetRotationMatrix2D(centre, angle, 1.0);

        double cos  = Math.Abs(rot.At<double>(0, 0));
        double sin  = Math.Abs(rot.At<double>(0, 1));
        int newW = (int)(src.Rows * sin + src.Cols * cos);
        int newH = (int)(src.Rows * cos + src.Cols * sin);

        rot.At<double>(0, 2) += (newW - src.Cols) / 2.0;
        rot.At<double>(1, 2) += (newH - src.Rows) / 2.0;

        var dst = new Mat();
        Cv2.WarpAffine(src, dst, rot, new Size(newW, newH),
            flags: InterpolationFlags.Linear,
            borderMode: BorderTypes.Replicate);
        return dst;
    }
}

