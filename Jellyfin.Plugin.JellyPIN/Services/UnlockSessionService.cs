using System.Collections.Concurrent;

namespace Jellyfin.Plugin.JellyPIN.Services;

public interface IUnlockSessionService
{
    DateTimeOffset Unlock(Guid userId, string deviceId, TimeSpan duration);
    bool IsUnlocked(Guid userId, string deviceId, out DateTimeOffset expiresAt);
    void Lock(Guid userId, string deviceId);
    void LockAll();
}

public sealed class UnlockSessionService(TimeProvider timeProvider) : IUnlockSessionService
{
    private readonly ConcurrentDictionary<(Guid UserId, string DeviceId), DateTimeOffset> _sessions = new();

    public DateTimeOffset Unlock(Guid userId, string deviceId, TimeSpan duration)
    {
        var expiry = timeProvider.GetUtcNow().Add(duration);
        _sessions[(userId, Normalize(deviceId))] = expiry;
        return expiry;
    }

    public bool IsUnlocked(Guid userId, string deviceId, out DateTimeOffset expiresAt)
    {
        var key = (userId, Normalize(deviceId));
        if (_sessions.TryGetValue(key, out expiresAt) && expiresAt > timeProvider.GetUtcNow()) return true;
        _sessions.TryRemove(key, out _);
        expiresAt = default;
        return false;
    }

    public void Lock(Guid userId, string deviceId) => _sessions.TryRemove((userId, Normalize(deviceId)), out _);

    public void LockAll() => _sessions.Clear();

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
