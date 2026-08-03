using System.Text.Json.Nodes;

namespace Jellyfin.Plugin.JellyPIN.Services;

public static class JellyPinJsonFilter
{
    public static int RemoveProtectedItems(JsonNode root, Func<Guid, bool> isProtected)
    {
        var removed = FilterChildren(root, isProtected);
        UpdateCounts(root);
        return removed;
    }

    private static int FilterChildren(JsonNode? node, Func<Guid, bool> isProtected)
    {
        var removed = 0;
        if (node is JsonArray array)
        {
            for (var index = array.Count - 1; index >= 0; index--)
            {
                var child = array[index];
                if (child is JsonObject item && TryGetItemId(item, out var itemId) && isProtected(itemId))
                {
                    array.RemoveAt(index);
                    removed++;
                }
                else
                {
                    removed += FilterChildren(child, isProtected);
                }
            }
        }
        else if (node is JsonObject jsonObject)
        {
            foreach (var child in jsonObject.ToArray())
            {
                removed += FilterChildren(child.Value, isProtected);
            }
        }

        return removed;
    }

    private static bool TryGetItemId(JsonObject item, out Guid itemId)
    {
        foreach (var property in item)
        {
            if ((string.Equals(property.Key, "Id", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Key, "ItemId", StringComparison.OrdinalIgnoreCase))
                && property.Value is JsonValue value
                && value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out itemId))
            {
                return true;
            }
        }

        itemId = Guid.Empty;
        return false;
    }

    private static void UpdateCounts(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            var resultArray = jsonObject.FirstOrDefault(property =>
                (string.Equals(property.Key, "Items", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.Key, "SearchHints", StringComparison.OrdinalIgnoreCase))
                && property.Value is JsonArray);
            if (resultArray.Value is JsonArray array)
            {
                var countKey = jsonObject.Select(property => property.Key).FirstOrDefault(key =>
                    string.Equals(key, "TotalRecordCount", StringComparison.OrdinalIgnoreCase));
                if (countKey is not null)
                {
                    jsonObject[countKey] = array.Count;
                }
            }

            foreach (var child in jsonObject.ToArray()) UpdateCounts(child.Value);
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var child in jsonArray) UpdateCounts(child);
        }
    }
}
