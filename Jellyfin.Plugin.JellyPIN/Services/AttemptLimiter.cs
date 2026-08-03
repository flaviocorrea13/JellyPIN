using System.Collections.Concurrent;

namespace Jellyfin.Plugin.JellyPIN.Services;

public readonly record struct AttemptDecision(bool Allowed, DateTimeOffset? LockedUntil);

public interface IAttemptLimiter
{
    AttemptDecision Check(Guid userId, string deviceId, int maximumAttempts, TimeSpan lockout);
    void RecordFailure(Guid userId, string deviceId, int maximumAttempts, TimeSpan lockout);
    void Reset(Guid userId, string deviceId);
    void ResetAll();
}

public sealed class AttemptLimiter(TimeProvider timeProvider) : IAttemptLimiter
{
    private sealed record State(int Failures, DateTimeOffset? LockedUntil);
    private readonly ConcurrentDictionary<(Guid, string), State> _states = new();

    public AttemptDecision Check(Guid userId, string deviceId, int maximumAttempts, TimeSpan lockout)
    {
        var key = Key(userId, deviceId);
        if (!_states.TryGetValue(key, out var state) || state.LockedUntil is null) return new(true, null);
        if (state.LockedUntil > timeProvider.GetUtcNow()) return new(false, state.LockedUntil);
        _states.TryRemove(key, out _);
        return new(true, null);
    }

    public void RecordFailure(Guid userId, string deviceId, int maximumAttempts, TimeSpan lockout)
    {
        var key = Key(userId, deviceId);
        _states.AddOrUpdate(key, _ => NewState(1, maximumAttempts, lockout), (_, old) => NewState(old.Failures + 1, maximumAttempts, lockout));
    }

    public void Reset(Guid userId, string deviceId) => _states.TryRemove(Key(userId, deviceId), out _);

    public void ResetAll() => _states.Clear();

    private State NewState(int failures, int maximum, TimeSpan lockout) =>
        new(failures, failures >= Math.Max(1, maximum) ? timeProvider.GetUtcNow().Add(lockout) : null);

    private static (Guid, string) Key(Guid userId, string deviceId) => (userId, deviceId.Trim().ToUpperInvariant());
}
