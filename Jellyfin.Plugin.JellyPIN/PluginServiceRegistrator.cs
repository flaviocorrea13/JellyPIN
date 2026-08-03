using Jellyfin.Plugin.JellyPIN.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.JellyPIN;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IPinHasher, PinHasher>();
        serviceCollection.AddSingleton<IUnlockSessionService, UnlockSessionService>();
        serviceCollection.AddSingleton<IAttemptLimiter, AttemptLimiter>();
        serviceCollection.AddSingleton<IProtectedItemService, ProtectedItemService>();
        serviceCollection.AddSingleton<IActiveProtectedRequestTracker, ActiveProtectedRequestTracker>();
        serviceCollection.AddSingleton<IProtectedPlaybackStopService, ProtectedPlaybackStopService>();
        serviceCollection.AddTransient<IStartupFilter, JellyPinStartupFilter>();
    }
}
