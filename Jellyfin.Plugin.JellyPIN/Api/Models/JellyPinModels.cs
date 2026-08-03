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
