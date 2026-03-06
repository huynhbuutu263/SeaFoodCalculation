namespace SeafoodVision.Domain.Enums;

/// <summary>
/// The shape type of a region of interest.
/// </summary>
public enum RegionType : byte
{
    /// <summary>Axis-aligned bounding rectangle (2 points: top-left, bottom-right).</summary>
    Rectangle = 0,

    /// <summary>Arbitrary polygon defined by N vertices (N ≥ 3).</summary>
    Polygon = 1
}
