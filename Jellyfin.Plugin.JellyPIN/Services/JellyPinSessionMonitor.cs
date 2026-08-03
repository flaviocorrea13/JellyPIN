using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyPIN.Services;

public sealed class JellyPinSessionMonitor(
    ISessionManager sessionManager,
    IProtectedItemService protectedItems,
    IUnlockSessionService sessions,
    IAuditService audit,
    ILogger<JellyPinSessionMonitor> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        sessionManager.PlaybackStart += OnPlaybackActivity;
        sessionManager.PlaybackProgress += OnPlaybackActivity;
        sessionManager.SessionEnded += OnSessionEnded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        sessionManager.PlaybackStart -= OnPlaybackActivity;
        sessionManager.PlaybackProgress -= OnPlaybackActivity;
        sessionManager.SessionEnded -= OnSessionEnded;
        return Task.CompletedTask;
    }

    private void OnPlaybackActivity(object? sender, PlaybackProgressEventArgs eventArgs)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration is null
            || eventArgs.Item is null
            || !protectedItems.IsProtected(eventArgs.Item, configuration.ProtectedLibraryId, configuration.ProtectedTag))
        {
            return;
        }

        var userId = eventArgs.Session?.UserId
            ?? eventArgs.Users.FirstOrDefault()?.Id
            ?? Guid.Empty;
        var deviceId = eventArgs.DeviceId ?? eventArgs.Session?.DeviceId;
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(deviceId)) return;

        if (!sessions.Refresh(
            userId,
            deviceId,
            TimeSpan.FromMinutes(configuration.UnlockDurationMinutes),
            out _)
            && eventArgs.Session is not null)
        {
            _ = StopExpiredPlaybackAsync(eventArgs.Session);
        }
    }

    private void OnSessionEnded(object? sender, SessionEventArgs eventArgs)
    {
        var session = eventArgs.SessionInfo;
        if (session is null || session.UserId == Guid.Empty || string.IsNullOrWhiteSpace(session.DeviceId)) return;

        sessions.Lock(session.UserId, session.DeviceId);
        audit.Record(
            AuditEventType.SessionEnded,
            session.UserId,
            session.UserName,
            session.DeviceId,
            session.DeviceName,
            session.Client,
            "Jellyfin session ended.");
    }

    private async Task StopExpiredPlaybackAsync(SessionInfo session)
    {
        try
        {
            await sessionManager.SendPlaystateCommand(
                session.Id,
                session.Id,
                new PlaystateRequest
                {
                    Command = PlaystateCommand.Stop,
                    ControllingUserId = session.UserId.ToString()
                },
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "JellyPIN could not stop expired protected playback on session {SessionId}", session.Id);
        }
    }
}
