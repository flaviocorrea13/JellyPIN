namespace Jellyfin.Plugin.JellyPIN.Api.Models;

public sealed record UnlockRequest(string Pin);

public sealed record SetPinRequest(string Pin);

public sealed record JellyPinStatusResponse(bool Configured, bool Unlocked, DateTimeOffset? ExpiresAt);

public sealed record UnlockResponse(bool Unlocked, DateTimeOffset ExpiresAt);

public sealed record ItemAccessResponse(
    Guid ItemId,
    bool Protected,
    bool Unlocked,
    bool Allowed,
    DateTimeOffset? ExpiresAt);

public sealed record LibraryScopeResponse(string Id, string Name, string[] Locations);

public sealed record UnlockSessionResponse(
    Guid UserId,
    string UserName,
    string DeviceId,
    string DeviceName,
    string Client,
    DateTimeOffset UnlockedAt,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt);

public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string Type,
    Guid? UserId,
    string UserName,
    string DeviceId,
    string DeviceName,
    string Client,
    string Detail);
