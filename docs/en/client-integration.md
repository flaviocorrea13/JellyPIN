# Native client integration

JellyPIN's server enforcement already protects requests from Android TV, Roku, Kodi, and other API clients. A native client adds usability by recognizing the locked response and presenting a PIN dialog.

## Required flow

1. Keep the client's Jellyfin device id stable and include it in every Jellyfin and JellyPIN request.
2. Before opening a protected library or after receiving a JellyPIN `403`, call `GET /JellyPIN/Status`.
3. Present a numeric 4–8 digit PIN dialog.
4. Call `POST /JellyPIN/Unlock` with `{ "pin": "1234" }` using the same user token and device id.
5. Retry the original navigation or playback request after a successful response.
6. Call `POST /JellyPIN/Lock` when the user chooses “Lock now.” Normal Jellyfin logout and session termination are also revoked by the server.

Locked server response:

```json
{
  "error": "JellyPINLocked",
  "message": "This content is protected by JellyPIN."
}
```

Clients should also treat HTTP `403` on a known protected navigation/playback attempt as locked, because native networking layers may not expose the JSON body.

## Android TV

- Intercept locked item-detail, image, playback-info, direct-play, and HLS responses in the common API error layer.
- Display a TV-friendly numeric dialog and keep the PIN only in view state until the unlock request completes.
- Never persist the PIN or include it in analytics/crash reports.
- Retry once after successful unlock; do not create an infinite retry loop.

## Roku

- Detect HTTP `403` in the task node responsible for item loading/playback.
- Show a numeric `StandardMessageDialog`/custom keypad scene.
- Submit the unlock request with the same `X-Emby-Authorization` device id used for Jellyfin requests.
- Clear the PIN field immediately and retry the original request once.

## Compatibility status

| Client | Server enforcement | Native PIN dialog |
|---|---:|---:|
| Jellyfin Web / browser | Yes | Included in JellyPIN Web patch |
| Jellyfin Media Player using patched Web | Yes | Included |
| Android TV official client | Yes | Client contribution required |
| Roku official client | Yes | Client contribution required |

## Remote unlock fallback

Administrators can use the JellyPIN dashboard to unlock one selected active device after verifying the PIN. This is the supported fallback for native clients, including Roku, that cannot install or display a JellyPIN-specific dialog. The action is rate-limited, audited, bound to the selected Jellyfin user and device id, and expires normally.

Client patches should be maintained in the respective upstream client repositories. Bundling modified official application binaries inside the server plugin would complicate signing, store distribution, upgrades, and security review.
