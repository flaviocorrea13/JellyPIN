using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class UnlockSessionServiceTests
{
    [Fact]
    public void Session_IsBoundToUserAndDeviceAndExpires()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new UnlockSessionService(clock);
        var user = Guid.NewGuid();
        sut.Unlock(user, "living-room", TimeSpan.FromMinutes(30));
        Assert.True(sut.IsUnlocked(user, "LIVING-ROOM", out _));
        Assert.False(sut.IsUnlocked(Guid.NewGuid(), "living-room", out _));
        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.False(sut.IsUnlocked(user, "living-room", out _));
    }

    [Fact]
    public void LockAll_RevokesEverySession()
    {
        var sut = new UnlockSessionService(TimeProvider.System);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        sut.Unlock(firstUser, "tv", TimeSpan.FromMinutes(30));
        sut.Unlock(secondUser, "phone", TimeSpan.FromMinutes(30));

        sut.LockAll();

        Assert.False(sut.IsUnlocked(firstUser, "tv", out _));
        Assert.False(sut.IsUnlocked(secondUser, "phone", out _));
    }

    [Fact]
    public void Refresh_ExtendsOnlyAnActiveSession()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new UnlockSessionService(clock);
        var user = Guid.NewGuid();
        var originalExpiry = sut.Unlock(user, "tv", TimeSpan.FromMinutes(10));

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(sut.Refresh(user, "tv", TimeSpan.FromMinutes(10), out var refreshedExpiry));
        Assert.Equal(originalExpiry.AddMinutes(5), refreshedExpiry);

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.False(sut.Refresh(user, "tv", TimeSpan.FromMinutes(10), out _));
    }

    [Fact]
    public void GetActiveSessions_ReturnsDeviceMetadataAndRemovesExpiredEntries()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new UnlockSessionService(clock);
        var user = Guid.NewGuid();
        sut.Unlock(user, "tv-id", TimeSpan.FromMinutes(10), "Parent", "Living Room", "Android TV");

        var active = Assert.Single(sut.GetActiveSessions());
        Assert.Equal("Parent", active.UserName);
        Assert.Equal("Living Room", active.DeviceName);
        Assert.Equal("Android TV", active.Client);

        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Empty(sut.GetActiveSessions());
    }
}
