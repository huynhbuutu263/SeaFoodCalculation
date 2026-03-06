using OpenCvSharp;

namespace SeafoodVision.Inspection.Core;

/// <summary>
/// Simple template matching utilities for visual inspection.
/// </summary>
public static class TemplateMatcher
{
    public sealed record MatchResult(Rect Region, double Score);

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
}

