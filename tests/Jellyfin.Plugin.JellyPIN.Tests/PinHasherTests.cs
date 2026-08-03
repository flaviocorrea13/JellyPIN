using Jellyfin.Plugin.JellyPIN.Services;

namespace Jellyfin.Plugin.JellyPIN.Tests;

public sealed class PinHasherTests
{
    [Fact]
    public void HashAndVerify_RoundTripsWithoutStoringPin()
    {
        var sut = new PinHasher();
        var hash = sut.Hash("1234");
        Assert.DoesNotContain("1234", hash, StringComparison.Ordinal);
        Assert.True(sut.Verify("1234", hash));
        Assert.False(sut.Verify("9999", hash));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("12a4")]
    public void Hash_RejectsInvalidPins(string pin) => Assert.Throws<ArgumentException>(() => new PinHasher().Hash(pin));
}

