# JellyPIN

![JellyPIN icon](assets/jellypin.png)

Developed by Flavio Correa ([@flaviocorrea13](https://www.instagram.com/flaviocorrea13)).

Project repository: [github.com/flaviocorrea13/JellyPIN](https://github.com/flaviocorrea13/JellyPIN)

JellyPIN is an experimental Jellyfin parental-control plugin. Version 0.1 provides the security primitives and authenticated API needed for temporary, per-user/per-device PIN unlock sessions.

Version 0.4 adds an ASP.NET Core request barrier registered by the plugin. When a request contains the id of the protected library or one of its descendants, JellyPIN denies locked requests before Jellyfin serves item metadata, images, downloads, subtitles, direct streams, or transcoding routes. This covers targeted item routes and queries containing item ids; broad discovery queries that contain no item or parent id still require a future Jellyfin query-layer integration for complete metadata filtering.

Version 0.5 filters protected items from common discovery JSON responses, including item lists, latest items, search hints, recommendations, and next-up results. Direct item and media routes remain forbidden while locked.

Administrators can call `POST /JellyPIN/LockAll` to revoke every temporary device unlock immediately. `POST /JellyPIN/Lock` continues to revoke only the calling device.

The global lock also sends `Stop` to sessions playing protected items and aborts active protected HTTP media requests.

## Plugin repository

After the GitHub repository and the matching release are published, Jellyfin administrators can add this catalog URL:

```text
https://raw.githubusercontent.com/flaviocorrea13/JellyPIN/main/manifest.json
```

Suggested repository name: `JellyPIN Repository`.

> **Important:** this MVP does not yet block Jellyfin media routes. Installing it alone does not prevent playback or hide protected items. See [Security boundary](#security-boundary).

## MVP 0.1

- Plugin configuration and an embedded administrative page
- PINs of 4–8 digits, stored only as PBKDF2-SHA256 hashes (210,000 iterations, random 128-bit salt)
- In-memory attempt limiting and temporary lockout
- In-memory unlock sessions bound to authenticated user and Jellyfin device id
- `GET /JellyPIN/Status`
- `POST /JellyPIN/Unlock` with `{ "pin": "1234" }`
- `POST /JellyPIN/Lock`
- `GET /JellyPIN/Items/{itemId}/Access`
- `GET /JellyPIN/Libraries`
- Basic unit tests

The configuration page sends a new PIN over the authenticated Jellyfin connection to an elevation-protected administrative endpoint, which hashes it on the server before saving configuration. The plaintext PIN is not persisted. Use HTTPS on untrusted networks because HTTP does not protect request contents in transit.

## Build

The current official plugin template targets `net9.0`, so this repository follows it. This MVP is currently pinned to Jellyfin Server 10.11.11 (target ABI 10.11.0.0). Install the .NET 9 SDK, then run:

```powershell
dotnet test JellyPIN.slnx
dotnet publish Jellyfin.Plugin.JellyPIN/Jellyfin.Plugin.JellyPIN.csproj -c Release
```

Copy the published plugin DLL into a dedicated folder below the Jellyfin plugin directory and restart the server. Match the Jellyfin NuGet package versions, target ABI, and target framework to the exact Jellyfin Server release used in production before installing.

`manifest.json` is the Jellyfin plugin catalog. Every release must update its release URL and MD5 checksum; the tag-based release workflow performs that update automatically.

## API identity

The JellyPIN API requires a valid Jellyfin-authenticated request. Unlock state is bound to the authenticated user claim and the Jellyfin device id. The controller accepts the device id from `X-Emby-Device-Id` or from the standard `X-Emby-Authorization` header.

State is deliberately in memory for 0.1. Restarting Jellyfin locks every session and clears failed-attempt counters (fail closed for unlock state). A later version can persist lockouts if restart-based evasion is considered in scope.

## Security boundary

### Achievable with a normal plugin

- Configuration page in the Jellyfin dashboard
- Secure PIN hashing and verification
- Custom authenticated REST endpoints
- Attempt throttling, temporary sessions, and audit services
- Reading library metadata and determining whether an item has the configured tag
- Client-side UI additions only when the client is separately modified or injects a supported extension

### Not enforced by this plugin MVP

A conventional Jellyfin plugin does not expose a documented, comprehensive authorization hook that is guaranteed to run for every item, image, subtitle, download, direct-play, HLS, and transcoding request. A modal or custom endpoint is therefore not a security boundary: an unmodified client could call Jellyfin's existing media routes directly.

Real enforcement needs one of these approaches:

1. **Preferred upstream/server patch:** introduce a core authorization policy/service invoked by every metadata and media route. JellyPIN can implement or consult that policy. This gives one server-side security boundary and can eventually become a reusable plugin extension point.
2. **Maintained Jellyfin Server fork:** patch all relevant query and delivery paths to consult JellyPIN unlock state. This works but creates an ongoing rebase and security-review burden.
3. **Reverse proxy gate:** feasible only with careful route coverage and server-side item/tag resolution; easy to bypass if any route is missed, so it is not the recommended design.

Jellyfin Web also needs a patch or supported client extension for the lock badge, PIN dialog, interception of navigation/play actions, and “lock now” command. That UI patch improves usability but cannot replace server enforcement. Native Android TV, Roku, Kodi, and other clients each require equivalent UX work, while the server patch remains authoritative.

## Next implementation slice

1. Add `ProtectedItemService` for the configured `jellypin` tag.
2. Prototype a Jellyfin Server authorization hook and enumerate every protected route with integration tests.
3. Add a Jellyfin Web PIN dialog that calls these endpoints.
4. Add audit events without logging PINs, hashes, or authentication tokens.

## Reference

The repository structure follows the official `jellyfin/jellyfin-plugin-template`. Jellyfin plugin binaries link against GPL-licensed Jellyfin packages, so this project uses GPL-3.0-or-later.
