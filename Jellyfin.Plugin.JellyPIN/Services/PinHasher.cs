using System.Security.Cryptography;

namespace Jellyfin.Plugin.JellyPIN.Services;

public sealed class PinHasher : IPinHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Algorithm = "PBKDF2-SHA256";

    public string Hash(string pin)
    {
        Validate(pin);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return string.Join('$', Algorithm, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string pin, string encodedHash)
    {
        if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(encodedHash)) return false;
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void Validate(string pin)
    {
        if (pin.Length is < 4 or > 8 || pin.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("PIN must contain 4 to 8 digits.", nameof(pin));
    }
}

