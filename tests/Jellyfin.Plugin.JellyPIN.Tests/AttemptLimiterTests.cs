using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class AttemptLimiterTests
{
    [Fact]
    public void Failures_TriggerAndThenReleaseLockout()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new AttemptLimiter(clock);
        var user = Guid.NewGuid();
        sut.RecordFailure(user, "tv", 2, TimeSpan.FromMinutes(10));
        Assert.True(sut.Check(user, "tv", 2, TimeSpan.FromMinutes(10)).Allowed);
        sut.RecordFailure(user, "tv", 2, TimeSpan.FromMinutes(10));
        Assert.False(sut.Check(user, "tv", 2, TimeSpan.FromMinutes(10)).Allowed);
        clock.Advance(TimeSpan.FromMinutes(11));
        Assert.True(sut.Check(user, "tv", 2, TimeSpan.FromMinutes(10)).Allowed);
    }

    [Fact]
    public void ResetAll_RemovesEveryLockout()
    {
        var sut = new AttemptLimiter(TimeProvider.System);
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        sut.RecordFailure(firstUser, "tv", 1, TimeSpan.FromMinutes(10));
        sut.RecordFailure(secondUser, "phone", 1, TimeSpan.FromMinutes(10));

        sut.ResetAll();

        Assert.True(sut.Check(firstUser, "tv", 1, TimeSpan.FromMinutes(10)).Allowed);
        Assert.True(sut.Check(secondUser, "phone", 1, TimeSpan.FromMinutes(10)).Allowed);
    }
}
