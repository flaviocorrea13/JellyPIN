using Jellyfin.Plugin.JellyPIN.Api.Models;
using Jellyfin.Plugin.JellyPIN.Services;
using MediaBrowser.Controller.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.JellyPIN.Api;

[ApiController]
[Authorize]
[Route("JellyPIN")]
public sealed partial class JellyPinController(
    IPinHasher pinHasher,
    IAttemptLimiter attemptLimiter,
    IUnlockSessionService sessions,
    IProtectedItemService protectedItems,
    ILibraryManager libraryManager,
    IActiveProtectedRequestTracker activeRequests,
    IProtectedPlaybackStopService playbackStopService,
    IAuditService audit,
    IAuthorizationContext authorizationContext) : ControllerBase
{
    [HttpPost("Pin")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult SetPin([FromBody] SetPinRequest request)
    {
        string hash;
        try
        {
            hash = pinHasher.Hash(request.Pin);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }

        var plugin = Plugin.Instance ?? throw new InvalidOperationException("JellyPIN is not initialized.");
        var configuration = plugin.Configuration;
        configuration.PinHash = hash;
        plugin.UpdateConfiguration(configuration);
        return NoContent();
    }

    [HttpDelete("Pin")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ResetPin()
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("JellyPIN is not initialized.");
        var configuration = plugin.Configuration;
        configuration.PinHash = string.Empty;
        plugin.UpdateConfiguration(configuration);
        attemptLimiter.ResetAll();
        sessions.LockAll();
        audit.Record(AuditEventType.PinReset, detail: "PIN reset by an administrator.");
        return NoContent();
    }

    [HttpGet("Status")]
    [ProducesResponseType<JellyPinStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JellyPinStatusResponse>> Status()
    {
        var config = GetConfiguration();
        var identity = await GetIdentityAsync().ConfigureAwait(false);
        var unlocked = sessions.IsUnlocked(identity.UserId, identity.DeviceId, out var expiry);
        return Ok(new JellyPinStatusResponse(
            !string.IsNullOrWhiteSpace(config.PinHash),
            unlocked,
            unlocked ? expiry : null));
    }

    [HttpPost("Unlock")]
    [ProducesResponseType<UnlockResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<UnlockResponse>> Unlock([FromBody] UnlockRequest request)
    {
        var config = GetConfiguration();
        if (string.IsNullOrWhiteSpace(config.PinHash)) return Problem("JellyPIN has not been configured.", statusCode: 409);

        var identity = await GetIdentityAsync().ConfigureAwait(false);
        var decision = attemptLimiter.Check(identity.UserId, identity.DeviceId, config.MaximumAttempts, TimeSpan.FromMinutes(config.LockoutMinutes));
        if (!decision.Allowed)
        {
            audit.Record(AuditEventType.UnlockFailed, identity.UserId, identity.UserName, identity.DeviceId, identity.DeviceName, identity.Client, "Attempt rejected during lockout.");
            if (decision.LockedUntil is { } lockedUntil) Response.Headers.RetryAfter = Math.Max(1, (int)(lockedUntil - DateTimeOffset.UtcNow).TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (!pinHasher.Verify(request.Pin, config.PinHash))
        {
            attemptLimiter.RecordFailure(identity.UserId, identity.DeviceId, config.MaximumAttempts, TimeSpan.FromMinutes(config.LockoutMinutes));
            audit.Record(AuditEventType.UnlockFailed, identity.UserId, identity.UserName, identity.DeviceId, identity.DeviceName, identity.Client, "Incorrect PIN.");
            return Unauthorized();
        }

        attemptLimiter.Reset(identity.UserId, identity.DeviceId);
        var expiry = sessions.Unlock(
            identity.UserId,
            identity.DeviceId,
            TimeSpan.FromMinutes(config.UnlockDurationMinutes),
            identity.UserName,
            identity.DeviceName,
            identity.Client);
        audit.Record(AuditEventType.UnlockSucceeded, identity.UserId, identity.UserName, identity.DeviceId, identity.DeviceName, identity.Client);
        return Ok(new UnlockResponse(true, expiry));
    }

    [HttpPost("Lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Lock()
    {
        var identity = await GetIdentityAsync().ConfigureAwait(false);
        sessions.Lock(identity.UserId, identity.DeviceId);
        audit.Record(AuditEventType.LockDevice, identity.UserId, identity.UserName, identity.DeviceId, identity.DeviceName, identity.Client);
        return NoContent();
    }

    [HttpPost("LockAll")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LockAll(CancellationToken cancellationToken)
    {
        sessions.LockAll();
        audit.Record(AuditEventType.LockAll, detail: "All devices locked by an administrator.");
        var configuration = GetConfiguration();
        activeRequests.AbortAll();
        await playbackStopService.StopAllAsync(
            configuration.ProtectedLibraryId,
            configuration.ProtectedTag,
            cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("Items/{itemId:guid}/Access")]
    [ProducesResponseType<ItemAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemAccessResponse>> ItemAccess(Guid itemId)
    {
        var identity = await GetIdentityAsync().ConfigureAwait(false);
        var item = libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return NotFound();
        }

        var configuration = GetConfiguration();
        var isProtected = protectedItems.IsProtected(item, configuration.ProtectedLibraryId, configuration.ProtectedTag);
        var expiry = default(DateTimeOffset);
        var unlocked = isProtected && sessions.IsUnlocked(identity.UserId, identity.DeviceId, out expiry);
        return Ok(new ItemAccessResponse(
            itemId,
            isProtected,
            unlocked,
            !isProtected || unlocked,
            unlocked ? expiry : null));
    }

    [HttpGet("Libraries")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType<LibraryScopeResponse[]>(StatusCodes.Status200OK)]
    public ActionResult<LibraryScopeResponse[]> Libraries()
    {
        var libraries = libraryManager.GetVirtualFolders()
            .Where(folder => !string.IsNullOrWhiteSpace(folder.ItemId))
            .Select(folder => new LibraryScopeResponse(folder.ItemId, folder.Name, folder.Locations))
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Ok(libraries);
    }

    [HttpGet("Sessions")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType<UnlockSessionResponse[]>(StatusCodes.Status200OK)]
    public ActionResult<UnlockSessionResponse[]> Sessions() => Ok(sessions.GetActiveSessions()
        .Select(session => new UnlockSessionResponse(
            session.UserId,
            session.UserName,
            session.DeviceId,
            session.DeviceName,
            session.Client,
            session.UnlockedAt,
            session.LastActivityAt,
            session.ExpiresAt))
        .ToArray());

    [HttpGet("Audit")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType<AuditEventResponse[]>(StatusCodes.Status200OK)]
    public ActionResult<AuditEventResponse[]> Audit([FromQuery] int limit = 100) => Ok(audit.GetRecent(limit)
        .Select(entry => new AuditEventResponse(
            entry.Id,
            entry.Timestamp,
            entry.Type.ToString(),
            entry.UserId,
            entry.UserName,
            entry.DeviceId,
            entry.DeviceName,
            entry.Client,
            entry.Detail))
        .ToArray());

    [HttpDelete("Audit")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearAudit()
    {
        audit.Clear();
        return NoContent();
    }

    private static Configuration.PluginConfiguration GetConfiguration() =>
        Plugin.Instance?.Configuration ?? throw new InvalidOperationException("JellyPIN is not initialized.");

    private async Task<RequestIdentity> GetIdentityAsync()
    {
        var info = await authorizationContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);
        if (!info.IsAuthenticated || info.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("The authenticated Jellyfin user id is unavailable.");
        var deviceId = info.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Request.Headers["X-Emby-Device-Id"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            var header = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                ?? Request.Headers.Authorization.FirstOrDefault()
                ?? string.Empty;
            var match = DeviceIdRegex().Match(header);
            deviceId = match.Success
                ? (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                : null;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
            throw new BadHttpRequestException("A Jellyfin device id is required.");
        return new RequestIdentity(
            info.UserId,
            info.User?.Username ?? string.Empty,
            deviceId,
            info.Device ?? string.Empty,
            info.Client ?? string.Empty);
    }

    private sealed record RequestIdentity(Guid UserId, string UserName, string DeviceId, string DeviceName, string Client);

    [GeneratedRegex("DeviceId=(?:\\\"([^\\\"]+)\\\"|([^,\\s]+))", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceIdRegex();
}
