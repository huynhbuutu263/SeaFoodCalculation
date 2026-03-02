namespace SeafoodVision.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier assigned by the tracker to a specific seafood item.
/// </summary>
public readonly struct TrackingId : IEquatable<TrackingId>
{
    public int Value { get; }

    public TrackingId(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "TrackingId must be non-negative.");
        Value = value;
    }

    public bool Equals(TrackingId other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is TrackingId other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => $"TID-{Value}";

    public static bool operator ==(TrackingId left, TrackingId right) => left.Equals(right);
    public static bool operator !=(TrackingId left, TrackingId right) => !left.Equals(right);
}
