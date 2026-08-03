using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyPIN.Services;

public interface IProtectedPlaybackStopService
{
    Task<int> StopAllAsync(string protectedLibraryId, string protectedTag, CancellationToken cancellationToken);
}

public sealed class ProtectedPlaybackStopService(
    ISessionManager sessionManager,
    ILibraryManager libraryManager,
    IProtectedItemService protectedItems,
    ILogger<ProtectedPlaybackStopService> logger) : IProtectedPlaybackStopService
{
    public async Task<int> StopAllAsync(string protectedLibraryId, string protectedTag, CancellationToken cancellationToken)
    {
        var stopped = 0;
        foreach (var session in sessionManager.Sessions.Where(session => session.NowPlayingItem is not null).ToArray())
        {
            var item = session.FullNowPlayingItem ?? libraryManager.GetItemById(session.NowPlayingItem.Id);
            if (item is null || !protectedItems.IsProtected(item, protectedLibraryId, protectedTag)) continue;

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
                    cancellationToken).ConfigureAwait(false);
                stopped++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "JellyPIN could not stop protected playback on session {SessionId}", session.Id);
            }
        }

        return stopped;
    }
}
