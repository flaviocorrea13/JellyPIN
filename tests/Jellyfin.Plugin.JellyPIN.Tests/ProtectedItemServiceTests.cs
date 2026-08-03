using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class ProtectedItemServiceTests
{
    [Fact]
    public void ProtectedTag_IsMatchedIgnoringCaseAndWhitespace()
    {
        string[] tags = ["Family", " JellyPIN "];

        Assert.True(ProtectedTagMatcher.IsMatch(tags, "jellypin"));
        Assert.False(ProtectedTagMatcher.IsMatch(tags, "private"));
    }

    [Fact]
    public void EmptyProtectedTag_DisablesProtection()
    {
        Assert.False(ProtectedTagMatcher.IsMatch(["jellypin"], " "));
    }

    [Fact]
    public void LibraryScope_ProtectsEveryDescendant()
    {
        var library = Guid.NewGuid();
        var folder = Guid.NewGuid();
        var movie = Guid.NewGuid();
        var parents = new Dictionary<Guid, Guid?>
        {
            [movie] = folder,
            [folder] = library,
            [library] = Guid.Empty
        };

        Assert.True(LibraryScopeMatcher.IsInScope(movie, id => parents.GetValueOrDefault(id), library));
        Assert.False(LibraryScopeMatcher.IsInScope(movie, id => parents.GetValueOrDefault(id), Guid.NewGuid()));
    }
}
