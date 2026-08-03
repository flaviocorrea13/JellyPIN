using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.JellyPIN.Services;

public interface IProtectedItemService
{
    bool IsProtected(BaseItem item, string protectedLibraryId, string protectedTag);
}

public sealed class ProtectedItemService(MediaBrowser.Controller.Library.ILibraryManager libraryManager) : IProtectedItemService
{
    public bool IsProtected(BaseItem item, string protectedLibraryId, string protectedTag)
    {
        if (Guid.TryParse(protectedLibraryId, out var libraryId)
            && (item.Id == libraryId
                || item.GetAncestorIds().Contains(libraryId)
                || LibraryScopeMatcher.IsInScope(item.Id, id => libraryManager.GetItemById(id)?.ParentId, libraryId)))
        {
            return true;
        }

        return ProtectedTagMatcher.IsMatch(item.Tags, protectedTag);
    }
}

public static class LibraryScopeMatcher
{
    public static bool IsInScope(Guid itemId, Func<Guid, Guid?> getParentId, Guid protectedLibraryId)
    {
        var current = itemId;
        var visited = new HashSet<Guid>();
        while (current != Guid.Empty && visited.Add(current))
        {
            if (current == protectedLibraryId)
            {
                return true;
            }

            current = getParentId(current) ?? Guid.Empty;
        }

        return false;
    }
}

public static class ProtectedTagMatcher
{
    public static bool IsMatch(IEnumerable<string> tags, string protectedTag)
    {
        if (string.IsNullOrWhiteSpace(protectedTag))
        {
            return false;
        }

        return tags.Any(tag => string.Equals(
            tag.Trim(),
            protectedTag.Trim(),
            StringComparison.OrdinalIgnoreCase));
    }
}
