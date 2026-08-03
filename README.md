# JellyPIN

![JellyPIN icon](assets/jellypin.png)

[Português](docs/pt-BR/README.md) · English

JellyPIN is parental-control software for Jellyfin that protects a complete media library with a PIN. It binds temporary unlocks to the authenticated Jellyfin user and device, hides protected discovery results, and enforces locked media requests on the server.

Developed by Flavio Correa ([@flaviocorrea13](https://www.instagram.com/flaviocorrea13)).

## Features

- Protect one complete Jellyfin library without tagging individual movies.
- Require the PIN for every user, including administrators.
- Bind each unlock to one user and one Jellyfin device id.
- Renew the expiration while protected content is actively accessed or played.
- Revoke an unlock when the Jellyfin session ends or logs out.
- Lock every device immediately and stop protected playback already in progress.
- List unlocked devices, their last protected activity, and expiration time.
- Persist the latest 1,000 audit events without recording PINs, hashes, or access tokens.
- Filter protected items from common item, latest, search, recommendation, and next-up responses.
- Block direct item, image, subtitle, download, direct-play, HLS, and transcoding requests that contain a protected item id.

## Installation

Jellyfin Server 10.11.11 and the .NET 9 runtime are currently supported.

1. Open **Dashboard → Plugins → Repositories**.
2. Add a repository named `JellyPIN Repository` with this URL:

   ```text
   https://raw.githubusercontent.com/flaviocorrea13/JellyPIN/main/manifest.json
   ```

3. Open the plugin catalog, install JellyPIN, and restart Jellyfin.
4. Open **Dashboard → Plugins → JellyPIN → Settings**.
5. Set a 4–8 digit PIN and choose the protected library.
6. Install the matching JellyPIN Web package when you want the browser PIN dialog and repository-link enhancement.

Upgrading Jellyfin Web can replace custom web files. Reinstall the matching JellyPIN Web package after a Jellyfin Web upgrade and never use a package built for a different Jellyfin Web version.

## Native clients

The server enforcement applies to Android TV and Roku: locked metadata and playback requests are rejected even if the client has no JellyPIN user interface. The browser/Jellyfin Web client includes the interactive PIN flow. The official native Android TV and Roku clients do not currently expose a plugin UI extension point, so a native PIN dialog requires a client-specific contribution. See [Native client integration](docs/en/client-integration.md).

This distinction is intentional: a client dialog improves usability, but the server remains the security boundary.

## API

All endpoints require a valid Jellyfin-authenticated request. Administrative endpoints additionally require Jellyfin elevation.

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/JellyPIN/Status` | Current device lock state |
| `POST` | `/JellyPIN/Unlock` | Verify a PIN and unlock the current device |
| `POST` | `/JellyPIN/Lock` | Lock the current device |
| `POST` | `/JellyPIN/LockAll` | Administratively lock every device and stop protected playback |
| `GET` | `/JellyPIN/Sessions` | List active unlock sessions |
| `GET` | `/JellyPIN/Devices` | Administratively list active Jellyfin devices and their JellyPIN state |
| `POST` | `/JellyPIN/Devices/Unlock` | Administratively unlock a selected device after verifying the PIN |
| `GET` | `/JellyPIN/Audit?limit=100` | Read recent audit events |
| `DELETE` | `/JellyPIN/Audit` | Clear audit history |
| `GET` | `/JellyPIN/Items/{id}/Access` | Check whether an item is protected and allowed |
| `GET` | `/JellyPIN/Libraries` | List selectable Jellyfin libraries |

Unlock body:

```json
{ "pin": "1234" }
```

Clients must supply their stable Jellyfin device id through the normal Jellyfin authorization header or `X-Emby-Device-Id`.

## Security notes

- The PIN is stored only as a PBKDF2-SHA256 hash with a random salt.
- Plaintext PINs, hashes, and Jellyfin tokens are never written to the audit history.
- Unlock sessions and attempt counters are intentionally memory-only; restarting Jellyfin fails closed and locks every device.
- Audit events are stored in `Jellyfin.Plugin.JellyPIN.audit.json` below Jellyfin's plugin configuration directory.
- Use HTTPS outside a trusted local network. HTTP exposes the PIN and Jellyfin token in transit.
- JellyPIN has not received an independent security audit. Read [SECURITY.md](SECURITY.md) before reporting a vulnerability.

## Build

```powershell
dotnet restore JellyPIN.slnx
dotnet test JellyPIN.slnx -c Release
dotnet publish Jellyfin.Plugin.JellyPIN/Jellyfin.Plugin.JellyPIN.csproj -c Release
```

The project follows the official Jellyfin plugin template and targets `net9.0`. Jellyfin package versions, target ABI, and the Jellyfin Web build must match the supported server release.

## Project status

JellyPIN is experimental. The request middleware enforces known metadata and media paths and filters common discovery responses, but it is not an upstream Jellyfin authorization framework. New Jellyfin routes require review and coverage tests. A future upstream authorization extension point would provide a stronger and more maintainable boundary than plugin middleware.

JellyPIN is licensed under GPL-3.0-or-later.
