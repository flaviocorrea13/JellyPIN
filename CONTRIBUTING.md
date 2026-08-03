# Contributing to JellyPIN

Thank you for helping improve JellyPIN.

1. Open an issue before large architectural or client-integration changes.
2. Do not include PINs, access tokens, private server addresses, or Jellyfin logs containing credentials.
3. Build with the .NET SDK version selected by `global.json` when present.
4. Run `dotnet test JellyPIN.slnx -c Release` before submitting a change.
5. Keep server-side enforcement fail-closed for protected direct-item and media routes.

Security-sensitive reports should follow [SECURITY.md](SECURITY.md) instead of being posted publicly.
