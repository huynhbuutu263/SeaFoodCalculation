using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SeafoodVision.Domain.Entities;
using SeafoodVision.Infrastructure.Data;
using SeafoodVision.Infrastructure.Data.Repositories;

namespace SeafoodVision.Infrastructure.Tests;

public sealed class SessionRepositoryTests : IDisposable
{
    private readonly SeafoodDbContext _context;
    private readonly SessionRepository _sut;

    public SessionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<SeafoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new SeafoodDbContext(options);
        _sut = new SessionRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsSavedSession()
    {
        var session = CountingSession.Start("CAM-01");
        await _sut.AddAsync(session);
        await _sut.SaveChangesAsync();

        var retrieved = await _sut.GetByIdAsync(session.Id);

        retrieved.Should().NotBeNull();
        retrieved!.CameraId.Should().Be("CAM-01");
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByStartedAtDescending()
    {
        var s1 = CountingSession.Start("CAM-01");
        await Task.Delay(5);
        var s2 = CountingSession.Start("CAM-02");

        await _sut.AddAsync(s1);
        await _sut.AddAsync(s2);
        await _sut.SaveChangesAsync();

        var recent = await _sut.GetRecentAsync(2);

        recent.Should().HaveCount(2);
        recent[0].CameraId.Should().Be("CAM-02");
    }

    public void Dispose() => _context.Dispose();
}
