using SeafoodVision.Domain.Enums;
using System.Drawing;

namespace SeafoodVision.Domain.ValueObjects;

/// <summary>
/// Defines a region of interest as either an axis-aligned rectangle or a free-form polygon.
/// All coordinates are normalised to [0, 1] relative to the frame dimensions so that
/// the ROI remains valid if the camera resolution changes.
/// </summary>
public sealed class RegionOfInterest
{
    /// <summary>Shape of this region.</summary>
    public RegionType RegionType { get; }

    /// <summary>
    /// Ordered vertices that define the region, normalised to [0, 1].
    /// For a <see cref="RegionType.Rectangle"/> exactly two points are stored:
    /// top-left and bottom-right.
    /// For a <see cref="RegionType.Polygon"/> three or more points are stored in order.
    /// </summary>
    public IReadOnlyList<PointF> Points { get; }

    public RegionOfInterest(RegionType regionType, IReadOnlyList<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (regionType == RegionType.Rectangle && points.Count != 2)
            throw new ArgumentException("A Rectangle ROI requires exactly 2 points (top-left, bottom-right).", nameof(points));

        if (regionType == RegionType.Polygon && points.Count < 3)
            throw new ArgumentException("A Polygon ROI requires at least 3 points.", nameof(points));

        RegionType = regionType;
        Points = points;
    }

    // ── Factory helpers ────────────────────────────────────────────────────────

    /// <summary>Creates a normalised rectangle ROI from two corners.</summary>
    public static RegionOfInterest FromRectangle(PointF topLeft, PointF bottomRight)
        => new(RegionType.Rectangle, [topLeft, bottomRight]);

    /// <summary>Creates a polygon ROI from an ordered list of vertices.</summary>
    public static RegionOfInterest FromPolygon(IReadOnlyList<PointF> vertices)
        => new(RegionType.Polygon, vertices);

    // ── Geometry helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Tests whether the normalised point (nx, ny) lies inside this region.
    /// Uses <c>System.Drawing.RectangleF</c> for rectangles and
    /// a ray-casting algorithm for polygons.
    /// </summary>
    public bool Contains(float nx, float ny)
    {
        if (RegionType == RegionType.Rectangle)
        {
            var tl = Points[0];
            var br = Points[1];
            return nx >= tl.X && nx <= br.X && ny >= tl.Y && ny <= br.Y;
        }

        // Ray-casting for arbitrary polygons
        bool inside = false;
        int n = Points.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var pi = Points[i];
            var pj = Points[j];
            if (pi.Y > ny != pj.Y > ny &&
                nx < (pj.X - pi.X) * (ny - pi.Y) / (pj.Y - pi.Y) + pi.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Converts the normalised region to a pixel-space <c>System.Drawing.Rectangle</c>
    /// (bounding box for polygons, exact rect for rectangles).
    /// </summary>
    public Rectangle ToPixelRect(int frameWidth, int frameHeight)
    {
        float minX = Points.Min(p => p.X);
        float minY = Points.Min(p => p.Y);
        float maxX = Points.Max(p => p.X);
        float maxY = Points.Max(p => p.Y);

        return new Rectangle(
            (int)(minX * frameWidth),
            (int)(minY * frameHeight),
            (int)((maxX - minX) * frameWidth),
            (int)((maxY - minY) * frameHeight));
    }
}
