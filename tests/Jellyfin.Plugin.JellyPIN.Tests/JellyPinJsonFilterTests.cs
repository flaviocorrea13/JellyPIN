using System.Text.Json.Nodes;
using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class JellyPinJsonFilterTests
{
    [Fact]
    public void RemoveProtectedItems_FiltersQueryResultAndUpdatesCount()
    {
        var allowed = Guid.NewGuid();
        var blocked = Guid.NewGuid();
        var json = JsonNode.Parse($$"""
        { "Items": [{ "Id": "{{allowed:N}}", "Name": "Livre" }, { "Id": "{{blocked:N}}", "Name": "Adulto" }], "TotalRecordCount": 2 }
        """)!;

        var removed = JellyPinJsonFilter.RemoveProtectedItems(json, id => id == blocked);

        Assert.Equal(1, removed);
        Assert.Single(json["Items"]!.AsArray());
        Assert.Equal(1, json["TotalRecordCount"]!.GetValue<int>());
    }

    [Fact]
    public void RemoveProtectedItems_FiltersSearchHintsByItemId()
    {
        var blocked = Guid.NewGuid();
        var json = JsonNode.Parse($$"""
        { "SearchHints": [{ "ItemId": "{{blocked}}", "Name": "Oculto" }], "TotalRecordCount": 1 }
        """)!;

        Assert.Equal(1, JellyPinJsonFilter.RemoveProtectedItems(json, id => id == blocked));
        Assert.Empty(json["SearchHints"]!.AsArray());
        Assert.Equal(0, json["TotalRecordCount"]!.GetValue<int>());
    }
}
