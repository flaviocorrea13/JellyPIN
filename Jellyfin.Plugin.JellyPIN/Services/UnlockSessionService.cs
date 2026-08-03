using System.Collections.Concurrent;

namespace Jellyfin.Plugin.JellyPIN.Services;

public sealed record UnlockSessionSnapshot(
    Guid UserId,
    string UserName,
    string DeviceId,
    string DeviceName,
    string Client,
    DateTimeOffset UnlockedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt);

public interface IUnlockSessionService
{
    DateTimeOffset Unlock(Guid userId, string deviceId, TimeSpan duration, string? userName = null, string? deviceName = null, string? client = null);
    bool IsUnlocked(Guid userId, string deviceId, out DateTimeOffset expiresAt);
    bool Refresh(Guid userId, string deviceId, TimeSpan duration, out DateTimeOffset expiresAt);
    void Lock(Guid userId, string deviceId);
    void LockAll();
    IReadOnlyList<UnlockSessionSnapshot> GetActiveSessions();
}

public sealed class UnlockSessionService(TimeProvider timeProvider) : IUnlockSessionService
{
    private sealed record Session(
        Guid UserId,
        string UserName,
        string DeviceId,
        string DeviceName,
        string Client,
        DateTimeOffset UnlockedAt,
        DateTimeOffset LastActivityAt,
        DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<(Guid UserId, string DeviceId), Session> _sessions = new();

    public DateTimeOffset Unlock(Guid userId, string deviceId, TimeSpan duration, string? userName = null, string? deviceName = null, string? client = null)
    {
        var now = timeProvider.GetUtcNow();
        var normalizedDeviceId = Normalize(deviceId);
        var session = new Session(
            userId,
            userName?.Trim() ?? string.Empty,
            normalizedDeviceId,
            deviceName?.Trim() ?? string.Empty,
            client?.Trim() ?? string.Empty,
            now,
            now,
            now.Add(duration));
        _sessions[(userId, normalizedDeviceId)] = session;
        return session.ExpiresAt;
    }

    public bool IsUnlocked(Guid userId, string deviceId, out DateTimeOffset expiresAt)
    {
        var key = (userId, Normalize(deviceId));
        if (_sessions.TryGetValue(key, out var session) && session.ExpiresAt > timeProvider.GetUtcNow())
        {
            expiresAt = session.ExpiresAt;
            return true;
        }

        _sessions.TryRemove(key, out _);
        expiresAt = default;
        return false;
    }

    public bool Refresh(Guid userId, string deviceId, TimeSpan duration, out DateTimeOffset expiresAt)
    {
        var key = (userId, Normalize(deviceId));
        var now = timeProvider.GetUtcNow();
        while (_sessions.TryGetValue(key, out var current))
        {
            if (current.ExpiresAt <= now)
            {
                _sessions.TryRemove(key, out _);
                break;
            }

            var updated = current with { LastActivityAt = now, ExpiresAt = now.Add(duration) };
            if (_sessions.TryUpdate(key, updated, current))
            {
                expiresAt = updated.ExpiresAt;
                return true;
            }
        }

        expiresAt = default;
        return false;
    }

    public void Lock(Guid userId, string deviceId) => _sessions.TryRemove((userId, Normalize(deviceId)), out _);

    public void LockAll() => _sessions.Clear();

    public IReadOnlyList<UnlockSessionSnapshot> GetActiveSessions()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in _sessions.Where(entry => entry.Value.ExpiresAt <= now).ToArray())
        {
            _sessions.TryRemove(entry.Key, out _);
        }

        return _sessions.Values
            .OrderBy(session => session.ExpiresAt)
            .Select(session => new UnlockSessionSnapshot(
                session.UserId,
                session.UserName,
                session.DeviceId,
                session.DeviceName,
                session.Client,
                session.UnlockedAt,
                session.LastActivityAt,
                session.ExpiresAt))
            .ToArray();
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
