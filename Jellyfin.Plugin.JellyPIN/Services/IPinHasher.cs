namespace Jellyfin.Plugin.JellyPIN.Services;

public interface IPinHasher
{
    string Hash(string pin);

    bool Verify(string pin, string encodedHash);
}

