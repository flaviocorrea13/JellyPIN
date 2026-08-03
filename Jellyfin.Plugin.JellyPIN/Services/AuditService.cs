using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyPIN.Services;

public enum AuditEventType
{
    UnlockSucceeded,
    UnlockFailed,
    LockDevice,
    LockAll,
    SessionEnded,
    PinReset
}

public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    AuditEventType Type,
    Guid? UserId,
    string UserName,
    string DeviceId,
    string DeviceName,
    string Client,
    string Detail);

public interface IAuditService
{
    void Record(AuditEventType type, Guid? userId = null, string? userName = null, string? deviceId = null, string? deviceName = null, string? client = null, string? detail = null);
    IReadOnlyList<AuditEvent> GetRecent(int limit);
    void Clear();
}

public sealed class AuditService : IAuditService
{
    private const int MaximumEvents = 1000;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditService>? _logger;
    private readonly string? _path;
    private readonly object _sync = new();
    private readonly List<AuditEvent> _events = [];

    public AuditService(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public AuditService(TimeProvider timeProvider, IApplicationPaths paths, ILogger<AuditService> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        _path = Path.Combine(paths.PluginConfigurationsPath, "Jellyfin.Plugin.JellyPIN.audit.json");
        Load();
    }

    public void Record(AuditEventType type, Guid? userId = null, string? userName = null, string? deviceId = null, string? deviceName = null, string? client = null, string? detail = null)
    {
        lock (_sync)
        {
            _events.Add(new AuditEvent(
                Guid.NewGuid(),
                _timeProvider.GetUtcNow(),
                type,
                userId,
                userName?.Trim() ?? string.Empty,
                deviceId?.Trim() ?? string.Empty,
                deviceName?.Trim() ?? string.Empty,
                client?.Trim() ?? string.Empty,
                detail?.Trim() ?? string.Empty));
            if (_events.Count > MaximumEvents) _events.RemoveRange(0, _events.Count - MaximumEvents);
            Save();
        }
    }

    public IReadOnlyList<AuditEvent> GetRecent(int limit)
    {
        lock (_sync)
        {
            return _events
                .AsEnumerable()
                .Reverse()
                .Take(Math.Clamp(limit, 1, MaximumEvents))
                .ToArray();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _events.Clear();
            Save();
        }
    }

    private void Load()
    {
        if (_path is null || !File.Exists(_path)) return;
        try
        {
            var events = JsonSerializer.Deserialize<List<AuditEvent>>(File.ReadAllText(_path));
            if (events is not null) _events.AddRange(events.TakeLast(MaximumEvents));
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "JellyPIN could not load its audit file {Path}", _path);
        }
    }

    private void Save()
    {
        if (_path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_events));
            File.Move(temporaryPath, _path, true);
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "JellyPIN could not persist its audit file {Path}", _path);
        }
    }
}
