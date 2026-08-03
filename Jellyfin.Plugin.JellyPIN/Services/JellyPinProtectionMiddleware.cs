using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyPIN.Services;

public sealed partial class JellyPinProtectionMiddleware(
    RequestDelegate next,
    ILibraryManager libraryManager,
    IProtectedItemService protectedItems,
    IUnlockSessionService sessions,
    IActiveProtectedRequestTracker activeRequests,
    IAuthorizationContext authorizationContext,
    ILogger<JellyPinProtectionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null
            || string.IsNullOrWhiteSpace(configuration.PinHash)
            || IsJellyPinEndpoint(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var filterDiscoveryResponse = ShouldFilterDiscoveryResponse(context.Request);
        var protectedItemId = FindProtectedItem(context, configuration.ProtectedLibraryId, configuration.ProtectedTag);
        if (protectedItemId is null && !filterDiscoveryResponse)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var authorization = await authorizationContext.GetAuthorizationInfo(context).ConfigureAwait(false);
        var deviceId = GetDeviceId(context, authorization.DeviceId);
        if (authorization.IsAuthenticated
            && authorization.UserId != Guid.Empty
            && !string.IsNullOrWhiteSpace(deviceId)
            && sessions.IsUnlocked(authorization.UserId, deviceId, out _))
        {
            if (protectedItemId is null)
            {
                await next(context).ConfigureAwait(false);
            }
            else
            {
                using var registration = activeRequests.Track(context);
                await next(context).ConfigureAwait(false);
            }

            return;
        }

        if (filterDiscoveryResponse)
        {
            await FilterDiscoveryResponseAsync(context, configuration.ProtectedLibraryId, configuration.ProtectedTag).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "JellyPIN blocked {Method} {Path} for protected item {ItemId}",
            context.Request.Method,
            context.Request.Path,
            protectedItemId);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "JellyPINLocked",
            message = "This content is protected by JellyPIN."
        }).ConfigureAwait(false);
    }

    private async Task FilterDiscoveryResponseAsync(HttpContext context, string protectedLibraryId, string protectedTag)
    {
        var originalBody = context.Response.Body;
        var originalAcceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        context.Request.Headers.AcceptEncoding = string.Empty;

        try
        {
            await next(context).ConfigureAwait(false);
            buffer.Position = 0;
            if (context.Response.StatusCode != StatusCodes.Status200OK
                || context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
            {
                await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            var root = await JsonNode.ParseAsync(buffer).ConfigureAwait(false);
            if (root is null)
            {
                buffer.Position = 0;
                await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            var removed = JellyPinJsonFilter.RemoveProtectedItems(root, itemId =>
            {
                var item = libraryManager.GetItemById(itemId);
                return item is not null && protectedItems.IsProtected(item, protectedLibraryId, protectedTag);
            });
            var output = JsonSerializer.SerializeToUtf8Bytes(root);
            context.Response.ContentLength = output.Length;
            context.Response.Headers.Remove("Content-Encoding");
            await originalBody.WriteAsync(output).ConfigureAwait(false);

            if (removed > 0)
            {
                logger.LogInformation("JellyPIN filtered {Count} protected item(s) from {Path}", removed, context.Request.Path);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
            context.Request.Headers.AcceptEncoding = originalAcceptEncoding;
        }
    }

    private Guid? FindProtectedItem(HttpContext context, string protectedLibraryId, string protectedTag)
    {
        foreach (var itemId in JellyPinRequestItems.Extract(context.Request.Path, context.Request.Query))
        {
            var item = libraryManager.GetItemById(itemId);
            if (item is not null && protectedItems.IsProtected(item, protectedLibraryId, protectedTag))
            {
                return itemId;
            }
        }

        return null;
    }

    private static bool IsJellyPinEndpoint(PathString path) =>
        path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "JellyPIN", StringComparison.OrdinalIgnoreCase)) == true;

    private static bool ShouldFilterDiscoveryResponse(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method)) return false;

        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;
        return path.EndsWith("/Items", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Items/Latest", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Search/Hints", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/Recommendations", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/NextUp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDeviceId(HttpContext context, string? authorizationDeviceId)
    {
        if (!string.IsNullOrWhiteSpace(authorizationDeviceId)) return authorizationDeviceId;

        var explicitDeviceId = context.Request.Headers["X-Emby-Device-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(explicitDeviceId)) return explicitDeviceId;

        var header = context.Request.Headers["X-Emby-Authorization"].FirstOrDefault()
            ?? context.Request.Headers.Authorization.FirstOrDefault()
            ?? string.Empty;
        var match = DeviceIdRegex().Match(header);
        return match.Success
            ? (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            : null;
    }

    [GeneratedRegex("DeviceId=(?:\\\"([^\\\"]+)\\\"|([^,\\s]+))", RegexOptions.IgnoreCase)]
    private static partial Regex DeviceIdRegex();
}
