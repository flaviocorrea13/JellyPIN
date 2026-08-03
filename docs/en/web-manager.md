# JellyPIN Web manager

The manager installs or updates JellyPIN Web on a Linux/LXC Jellyfin installation. Before changing files, it verifies the installed Jellyfin Web version, validates the official release SHA-256 checksum, rejects unsafe ZIP entries, preserves `config.json`, and creates a complete timestamped backup.

The directory replacement only happens after the staged package passes validation. If Jellyfin does not restart successfully, the previous Web directory is restored automatically.

## Download and inspect

```bash
curl -fL \
  https://raw.githubusercontent.com/flaviocorrea13/JellyPIN/main/scripts/jellypin-web-manager.sh \
  -o /tmp/jellypin-web-manager.sh

less /tmp/jellypin-web-manager.sh
chmod +x /tmp/jellypin-web-manager.sh
```

## Install or update

```bash
sudo /tmp/jellypin-web-manager.sh install
sudo /tmp/jellypin-web-manager.sh install 0.8.0.0
```

The operation stops when the installed Jellyfin Web version differs from `10.11.11`. Do not use `--force-version` unless package compatibility has been verified manually.

## Status, backups, and restore

```bash
/tmp/jellypin-web-manager.sh status
/tmp/jellypin-web-manager.sh backups
sudo /tmp/jellypin-web-manager.sh restore
sudo /tmp/jellypin-web-manager.sh restore 20260803T120000Z-pre-install
```

Backups are stored in `/var/lib/jellypin/web-backups`. Restoring also creates a `pre-restore` backup of the current state, so the restore itself remains reversible.

## Custom installation paths

```bash
sudo env \
  JELLYPIN_WEB_DIR=/custom/jellyfin/web \
  JELLYPIN_BACKUP_DIR=/custom/backup/path \
  JELLYPIN_SERVICE=jellyfin \
  /tmp/jellypin-web-manager.sh install 0.8.0.0
```

The backup directory must never be located inside the Jellyfin Web directory.
