namespace SeafoodVision.Domain.ValueObjects;

/// <summary>
/// Represents a 2D bounding box returned by the detection model.
/// </summary>
public sealed record BoundingBox(float X, float Y, float Width, float Height)
{
    public float CenterX => X + Width / 2f;
    public float CenterY => Y + Height / 2f;
    public float Area => Width * Height;

    public bool Overlaps(BoundingBox other)
    {
        float ix1 = MathF.Max(X, other.X);
        float iy1 = MathF.Max(Y, other.Y);
        float ix2 = MathF.Min(X + Width, other.X + other.Width);
        float iy2 = MathF.Min(Y + Height, other.Y + other.Height);
        return ix2 > ix1 && iy2 > iy1;
    }

    public float IntersectionOverUnion(BoundingBox other)
    {
        float ix1 = MathF.Max(X, other.X);
        float iy1 = MathF.Max(Y, other.Y);
        float ix2 = MathF.Min(X + Width, other.X + other.Width);
        float iy2 = MathF.Min(Y + Height, other.Y + other.Height);
        if (ix2 <= ix1 || iy2 <= iy1) return 0f;
        float intersection = (ix2 - ix1) * (iy2 - iy1);
        float union = Area + other.Area - intersection;
        return union <= 0f ? 0f : intersection / union;
    }
}
