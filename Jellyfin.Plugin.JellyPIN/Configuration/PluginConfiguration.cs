using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyPIN.Configuration;

public sealed class PluginConfiguration : BasePluginConfiguration
{
    public string PinHash { get; set; } = string.Empty;

    public int UnlockDurationMinutes { get; set; } = 30;

    public int MaximumAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 10;

    public string ProtectedTag { get; set; } = "jellypin";

    public string ProtectedLibraryId { get; set; } = string.Empty;
}
