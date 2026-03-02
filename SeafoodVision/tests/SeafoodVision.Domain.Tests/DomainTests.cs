using FluentAssertions;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Domain.Tests;

public sealed class BoundingBoxTests
{
    [Fact]
    public void IntersectionOverUnion_SameBox_ReturnsOne()
    {
        var box = new BoundingBox(10, 10, 50, 50);
        box.IntersectionOverUnion(box).Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void IntersectionOverUnion_NoOverlap_ReturnsZero()
    {
        var a = new BoundingBox(0, 0, 10, 10);
        var b = new BoundingBox(20, 20, 10, 10);
        a.IntersectionOverUnion(b).Should().Be(0f);
    }

    [Fact]
    public void IntersectionOverUnion_PartialOverlap_BetweenZeroAndOne()
    {
        var a = new BoundingBox(0, 0, 10, 10);
        var b = new BoundingBox(5, 5, 10, 10);
        float iou = a.IntersectionOverUnion(b);
        iou.Should().BeGreaterThan(0f).And.BeLessThan(1f);
    }

    [Fact]
    public void CenterX_ReturnsCorrectValue()
    {
        var box = new BoundingBox(10, 10, 20, 20);
        box.CenterX.Should().Be(20f);
    }
}

public sealed class SeafoodItemTests
{
    [Fact]
    public void MarkAsCounted_SetsIsCounted()
    {
        var item = new SeafoodItem(
            new TrackingId(1), "salmon", 0.95f,
            new BoundingBox(0, 0, 10, 10), DateTime.UtcNow);

        item.MarkAsCounted();

        item.IsCounted.Should().BeTrue();
    }

    [Fact]
    public void UpdatePosition_UpdatesBoundingBoxAndTimestamp()
    {
        var now = DateTime.UtcNow;
        var item = new SeafoodItem(new TrackingId(1), "tuna", 0.9f,
            new BoundingBox(0, 0, 10, 10), now);
        var newBox = new BoundingBox(5, 5, 10, 10);
        var later = now.AddMilliseconds(33);

        item.UpdatePosition(newBox, later);

        item.BoundingBox.Should().Be(newBox);
        item.LastSeenAt.Should().Be(later);
    }

    [Fact]
    public void CountingSession_IncrementCount_IncreasesTotalCount()
    {
        var session = CountingSession.Start("CAM-01");
        session.IncrementCount();
        session.IncrementCount(3);
        session.TotalCount.Should().Be(4);
    }

    [Fact]
    public void CountingSession_End_SetsEndedAt()
    {
        var session = CountingSession.Start("CAM-01");
        session.End();
        session.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public void CountingSession_EndTwice_ThrowsInvalidOperationException()
    {
        var session = CountingSession.Start("CAM-01");
        session.End();
        session.Invoking(s => s.End()).Should().Throw<InvalidOperationException>();
    }
}
