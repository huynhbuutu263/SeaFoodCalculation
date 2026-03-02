using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SeafoodVision.Application.Services;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Domain.ValueObjects;

namespace SeafoodVision.Application.Tests;

public sealed class TrackingServiceTests
{
    private readonly TrackingService _sut = new(NullLogger<TrackingService>.Instance);

    private static SeafoodItem MakeItem(int tmpId, float x, float y) =>
        new(new TrackingId(tmpId), "salmon", 0.9f,
            new BoundingBox(x, y, 20, 20), DateTime.UtcNow);

    [Fact]
    public async Task Update_NewDetection_AssignsNewTrackingId()
    {
        var detections = new[] { MakeItem(0, 10, 10) };
        var result = await _sut.UpdateAsync(detections, DateTime.UtcNow);
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Update_SameDetectionTwice_ReturnsSingleTrack()
    {
        var now = DateTime.UtcNow;
        var detection = new[] { MakeItem(0, 10, 10) };
        await _sut.UpdateAsync(detection, now);
        var result = await _sut.UpdateAsync(detection, now.AddMilliseconds(33));
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Update_NonOverlappingBoxes_ReturnsTwoTracks()
    {
        var items = new[]
        {
            MakeItem(0, 0, 0),
            MakeItem(1, 500, 500)
        };
        var result = await _sut.UpdateAsync(items, DateTime.UtcNow);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Update_StaleTrack_IsRemovedAfterMaxAge()
    {
        // Use a tracker with a 1-second max age
        var sut = new TrackingService(NullLogger<TrackingService>.Instance, maxAge: TimeSpan.FromSeconds(1));

        // Add a detection at an old timestamp (10 seconds ago)
        var old = new[] { MakeItem(0, 10, 10) };
        await sut.UpdateAsync(old, DateTime.UtcNow.AddSeconds(-10));

        // Update with a new non-overlapping detection at the current time; the old track should be evicted
        var fresh = new[] { MakeItem(1, 500, 500) };
        var result = await sut.UpdateAsync(fresh, DateTime.UtcNow);
        result.Should().HaveCount(1);
    }
}
