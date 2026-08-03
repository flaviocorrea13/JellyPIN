using Jellyfin.Plugin.JellyPIN.Services;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class JellyPinRequestItemsTests
{
    [Fact]
    public void Extract_FindsIdsInPathAndQuery()
    {
        var pathId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Path = $"/Videos/{pathId}/stream";
        context.Request.QueryString = new QueryString($"?ParentId={parentId:N}");

        var result = JellyPinRequestItems.Extract(context.Request.Path, context.Request.Query);

        Assert.Contains(pathId, result);
        Assert.Contains(parentId, result);
    }

    [Fact]
    public void Extract_IgnoresNonGuidValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Items/Latest";
        context.Request.QueryString = new QueryString("?IncludeItemTypes=Movie&Recursive=true");

        Assert.Empty(JellyPinRequestItems.Extract(context.Request.Path, context.Request.Query));
    }
}
