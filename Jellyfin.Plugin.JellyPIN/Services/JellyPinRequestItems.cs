using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.JellyPIN.Services;

public static class JellyPinRequestItems
{
    public static IReadOnlySet<Guid> Extract(PathString path, IQueryCollection query)
    {
        var ids = new HashSet<Guid>();
        AddValues(path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [], ids);

        foreach (var parameter in query)
        {
            foreach (var value in parameter.Value)
            {
                AddValues(value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [], ids);
            }
        }

        return ids;
    }

    private static void AddValues(IEnumerable<string> values, ISet<Guid> ids)
    {
        foreach (var value in values)
        {
            if (Guid.TryParse(value, out var id) && id != Guid.Empty)
            {
                ids.Add(id);
            }
        }
    }
}
