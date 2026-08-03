#!/usr/bin/env bash
set -Eeuo pipefail

readonly PROJECT="JellyPIN"
readonly REPOSITORY="flaviocorrea13/JellyPIN"
readonly SUPPORTED_WEB_VERSION="10.11.11"
readonly DEFAULT_WEB_DIR="/usr/share/jellyfin/web"
readonly DEFAULT_BACKUP_DIR="/var/lib/jellypin/web-backups"
readonly DEFAULT_SERVICE="jellyfin"

WEB_DIR="${JELLYPIN_WEB_DIR:-$DEFAULT_WEB_DIR}"
BACKUP_DIR="${JELLYPIN_BACKUP_DIR:-$DEFAULT_BACKUP_DIR}"
SERVICE_NAME="${JELLYPIN_SERVICE:-$DEFAULT_SERVICE}"
FORCE_VERSION=false
TEMP_PATHS=()

log() { printf '[%s] %s\n' "$PROJECT" "$*"; }
fail() { printf '[%s] ERROR: %s\n' "$PROJECT" "$*" >&2; exit 1; }

cleanup() {
    local path
    for path in "${TEMP_PATHS[@]:-}"; do
        [[ -n "$path" && -e "$path" ]] || continue
        case "$path" in
            /tmp/jellypin-web.*|"$(dirname "$WEB_DIR")"/.jellypin-web-stage.*)
                rm -rf -- "$path"
                ;;
        esac
    done
}
trap cleanup EXIT

usage() {
    cat <<'EOF'
JellyPIN Web installer and recovery manager

Usage:
  sudo ./jellypin-web-manager.sh install [VERSION] [--force-version]
  sudo ./jellypin-web-manager.sh restore [BACKUP_ID]
  sudo ./jellypin-web-manager.sh backups
  sudo ./jellypin-web-manager.sh status

VERSION defaults to the latest JellyPIN GitHub release.
BACKUP_ID defaults to the newest valid backup.

Environment overrides:
  JELLYPIN_WEB_DIR       Jellyfin Web directory
  JELLYPIN_BACKUP_DIR    Backup storage directory
  JELLYPIN_SERVICE       systemd service name
EOF
}

require_root() {
    [[ "${EUID}" -eq 0 ]] || fail "Run this command with sudo."
}

require_commands() {
    local command
    for command in curl sha256sum unzip tar systemctl mktemp flock sed grep find readlink awk; do
        command -v "$command" >/dev/null 2>&1 || fail "Required command not found: $command"
    done
}

validate_paths() {
    [[ "$WEB_DIR" = /* && "$WEB_DIR" != "/" ]] || fail "Invalid Jellyfin Web directory: $WEB_DIR"
    [[ "$(basename "$WEB_DIR")" = "web" ]] || fail "For safety, the Jellyfin Web directory must end in /web."
    [[ "$BACKUP_DIR" = /* && "$BACKUP_DIR" != "/" ]] || fail "Invalid backup directory: $BACKUP_DIR"
    [[ "$BACKUP_DIR/" != "$WEB_DIR/"* ]] || fail "The backup directory cannot be inside the Jellyfin Web directory."
    [[ -d "$WEB_DIR" && -f "$WEB_DIR/index.html" ]] || fail "Jellyfin Web was not found at $WEB_DIR"
    [[ -f "$WEB_DIR/config.json" ]] || fail "The current Jellyfin Web config.json was not found."
}

installed_web_version() {
    local package_version=""
    if command -v dpkg-query >/dev/null 2>&1; then
        package_version="$(dpkg-query -W -f='${Version}' jellyfin-web 2>/dev/null || true)"
    fi
    if [[ "$package_version" =~ ([0-9]+\.[0-9]+\.[0-9]+) ]]; then
        printf '%s\n' "${BASH_REMATCH[1]}"
        return
    fi
    printf 'unknown\n'
}

check_web_version() {
    local installed
    installed="$(installed_web_version)"
    if [[ "$installed" = "$SUPPORTED_WEB_VERSION" ]]; then
        return
    fi
    if [[ "$FORCE_VERSION" = true ]]; then
        log "Warning: installed Jellyfin Web version is '$installed'; forcing a package for $SUPPORTED_WEB_VERSION."
        return
    fi
    fail "Installed Jellyfin Web version is '$installed', but this package requires $SUPPORTED_WEB_VERSION. Use --force-version only after verifying compatibility."
}

latest_version() {
    local tag
    tag="$(curl -fsSL --retry 3 --connect-timeout 15 \
        -H 'Accept: application/vnd.github+json' \
        "https://api.github.com/repos/$REPOSITORY/releases/latest" \
        | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"v\{0,1\}\([^"]*\)".*/\1/p' \
        | head -n 1)"
    [[ "$tag" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "Could not determine the latest JellyPIN version."
    printf '%s\n' "$tag"
}

validate_version() {
    [[ "$1" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "Invalid JellyPIN version: $1"
}

validate_archive_entries() {
    local archive="$1" entry
    while IFS= read -r entry; do
        [[ -n "$entry" ]] || continue
        [[ "$entry" != /* && "$entry" != ../* && "$entry" != *'/../'* && "$entry" != *'\\..\\'* && ! "$entry" =~ ^[A-Za-z]: ]] \
            || fail "Unsafe path found inside the Web package: $entry"
    done < <(unzip -Z1 "$archive")
}

create_backup() {
    local reason="$1" version="${2:-unknown}" timestamp backup_id target
    timestamp="$(date -u +'%Y%m%dT%H%M%SZ')"
    backup_id="${timestamp}-${reason}"
    target="$BACKUP_DIR/$backup_id"
    mkdir -p -- "$target"
    tar -C "$(dirname "$WEB_DIR")" -czpf "$target/web.tar.gz" "$(basename "$WEB_DIR")"
    cat > "$target/metadata" <<EOF
created_at=$timestamp
reason=$reason
jellypin_version=$version
jellyfin_web_version=$(installed_web_version)
web_dir=$WEB_DIR
EOF
    ln -sfn -- "$backup_id" "$BACKUP_DIR/latest"
    printf '%s\n' "$backup_id"
}

verify_staged_web() {
    local stage="$1"
    [[ -f "$stage/index.html" ]] || fail "The staged package does not contain index.html."
    [[ -f "$stage/config.json" ]] || fail "The staged package does not contain config.json."
    find "$stage" -type f -name '*.js' -print -quit | grep -q . || fail "The staged package does not contain JavaScript assets."
}

activate_stage() {
    local stage="$1" rollback parent
    parent="$(dirname "$WEB_DIR")"
    rollback="$parent/.jellypin-web-rollback.$(date -u +'%Y%m%dT%H%M%SZ')"
    [[ ! -e "$rollback" ]] || fail "Rollback path already exists: $rollback"

    systemctl stop "$SERVICE_NAME"
    mv -- "$WEB_DIR" "$rollback"
    if ! mv -- "$stage" "$WEB_DIR"; then
        mv -- "$rollback" "$WEB_DIR"
        systemctl start "$SERVICE_NAME" || true
        fail "Could not activate the staged Web directory; the previous version was restored."
    fi

    if ! systemctl start "$SERVICE_NAME" || ! systemctl is-active --quiet "$SERVICE_NAME"; then
        systemctl stop "$SERVICE_NAME" || true
        rm -rf -- "$WEB_DIR"
        mv -- "$rollback" "$WEB_DIR"
        systemctl start "$SERVICE_NAME" || true
        fail "Jellyfin did not start successfully; the previous Web directory was restored automatically."
    fi

    rm -rf -- "$rollback"
}

install_web() {
    local requested_version="${1:-latest}" version package checksum_name release_url work archive checksum stage backup_id expected_hash actual_hash
    [[ "${2:-}" = "--force-version" || "${1:-}" = "--force-version" ]] && FORCE_VERSION=true
    [[ "$requested_version" = "--force-version" ]] && requested_version="latest"
    check_web_version
    version="$requested_version"
    [[ "$version" = "latest" ]] && version="$(latest_version)"
    validate_version "$version"

    package="JellyPIN_Web_${version}_Jellyfin_${SUPPORTED_WEB_VERSION}.zip"
    checksum_name="${package}.sha256"
    release_url="https://github.com/$REPOSITORY/releases/download/v${version}"
    work="$(mktemp -d /tmp/jellypin-web.XXXXXX)"
    TEMP_PATHS+=("$work")
    archive="$work/$package"
    checksum="$work/$checksum_name"

    log "Downloading JellyPIN Web $version..."
    curl -fL --retry 3 --connect-timeout 15 -o "$archive" "$release_url/$package"
    curl -fL --retry 3 --connect-timeout 15 -o "$checksum" "$release_url/$checksum_name"
    expected_hash="$(awk 'NR == 1 { print $1 }' "$checksum")"
    [[ "$expected_hash" =~ ^[0-9a-fA-F]{64}$ ]] || fail "The published SHA-256 file is invalid. No files were changed."
    actual_hash="$(sha256sum "$archive" | awk '{ print $1 }')"
    [[ "${actual_hash,,}" = "${expected_hash,,}" ]] || fail "SHA-256 validation failed. No files were changed."
    validate_archive_entries "$archive"

    stage="$(mktemp -d "$(dirname "$WEB_DIR")/.jellypin-web-stage.XXXXXX")"
    TEMP_PATHS+=("$stage")
    unzip -q "$archive" -d "$stage"
    cp -a -- "$WEB_DIR/config.json" "$stage/config.json"
    chown -R --reference="$WEB_DIR" "$stage"
    verify_staged_web "$stage"

    backup_id="$(create_backup "pre-install" "$version")"
    log "Backup created: $backup_id"
    activate_stage "$stage"
    log "JellyPIN Web $version installed successfully."
}

resolve_backup() {
    local requested="${1:-latest}" resolved
    if [[ "$requested" = "latest" ]]; then
        [[ -L "$BACKUP_DIR/latest" ]] || fail "No latest backup was found."
        resolved="$(readlink "$BACKUP_DIR/latest")"
    else
        resolved="$requested"
    fi
    [[ "$resolved" =~ ^[0-9]{8}T[0-9]{6}Z-[a-z-]+$ ]] || fail "Invalid backup id: $resolved"
    [[ -f "$BACKUP_DIR/$resolved/web.tar.gz" ]] || fail "Backup not found: $resolved"
    printf '%s\n' "$resolved"
}

restore_web() {
    local backup_id source stage extracted current_backup
    backup_id="$(resolve_backup "${1:-latest}")"
    source="$BACKUP_DIR/$backup_id/web.tar.gz"
    stage="$(mktemp -d "$(dirname "$WEB_DIR")/.jellypin-web-stage.XXXXXX")"
    TEMP_PATHS+=("$stage")
    tar -xzpf "$source" -C "$stage"
    extracted="$stage/$(basename "$WEB_DIR")"
    verify_staged_web "$extracted"
    current_backup="$(create_backup "pre-restore" "unknown")"
    log "Current Web directory backed up as: $current_backup"
    activate_stage "$extracted"
    log "Backup $backup_id restored successfully."
}

list_backups() {
    local directory
    [[ -d "$BACKUP_DIR" ]] || { log "No backups found."; return; }
    for directory in "$BACKUP_DIR"/*; do
        [[ -d "$directory" && -f "$directory/web.tar.gz" ]] || continue
        printf '%s\n' "$(basename "$directory")"
        [[ -f "$directory/metadata" ]] && sed 's/^/  /' "$directory/metadata"
    done
}

show_status() {
    printf 'Jellyfin service: %s\n' "$(systemctl is-active "$SERVICE_NAME" 2>/dev/null || true)"
    printf 'Jellyfin Web directory: %s\n' "$WEB_DIR"
    printf 'Installed Jellyfin Web version: %s\n' "$(installed_web_version)"
    if [[ -L "$BACKUP_DIR/latest" ]]; then
        printf 'Latest backup: %s\n' "$(readlink "$BACKUP_DIR/latest")"
    else
        printf 'Latest backup: none\n'
    fi
}

main() {
    local action="${1:-}"
    shift || true
    case "$action" in
        install|restore)
            require_root
            require_commands
            validate_paths
            mkdir -p -- "$BACKUP_DIR"
            exec 9>"/run/lock/jellypin-web-manager.lock"
            flock -n 9 || fail "Another JellyPIN Web operation is already running."
            "$action"_web "$@"
            ;;
        backups)
            list_backups
            ;;
        status)
            show_status
            ;;
        -h|--help|help|'')
            usage
            ;;
        *)
            usage >&2
            fail "Unknown action: $action"
            ;;
    esac
}

main "$@"
