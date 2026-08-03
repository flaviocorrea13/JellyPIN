using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class AuditServiceTests
{
    [Fact]
    public void RecordsNewestEventsFirstAndCanClear()
    {
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var sut = new AuditService(clock);
        sut.Record(AuditEventType.UnlockFailed, deviceId: "tv", detail: "Incorrect PIN.");
        clock.Advance(TimeSpan.FromSeconds(1));
        sut.Record(AuditEventType.UnlockSucceeded, deviceId: "tv");

        var events = sut.GetRecent(10);
        Assert.Equal(2, events.Count);
        Assert.Equal(AuditEventType.UnlockSucceeded, events[0].Type);
        Assert.Equal(AuditEventType.UnlockFailed, events[1].Type);

        sut.Clear();
        Assert.Empty(sut.GetRecent(10));
    }
}
