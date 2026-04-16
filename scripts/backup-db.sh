#!/usr/bin/env bash
# ============================================================
# AykutOnPC — Automated PostgreSQL Backup
# Cron: 0 3 * * * /opt/aykutonpc/scripts/backup-db.sh >> /var/log/aykutonpc-backup.log 2>&1
# ============================================================
set -euo pipefail

BACKUP_DIR="/opt/aykutonpc/backups"
CONTAINER_NAME="aykutonpc-db"
DB_NAME="AykutOnPC_Db"
DB_USER="postgres"
RETENTION_DAYS=7
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.sql.gz"

mkdir -p "$BACKUP_DIR"
echo "[$(date -Iseconds)] INFO  Starting backup → ${BACKUP_FILE}"

# Dump inside container, stream + gzip to host
docker exec "$CONTAINER_NAME" \
    pg_dump -U "$DB_USER" -d "$DB_NAME" \
    --no-owner --no-privileges --clean --if-exists \
    | gzip > "$BACKUP_FILE"

if [[ ! -s "$BACKUP_FILE" ]]; then
    echo "[$(date -Iseconds)] ERROR Backup file is empty! Removing and aborting."
    rm -f "$BACKUP_FILE"
    exit 1
fi

BACKUP_SIZE=$(du -sh "$BACKUP_FILE" | cut -f1)
echo "[$(date -Iseconds)] INFO  Backup complete. Size=${BACKUP_SIZE}"

# ── Retention cleanup ────────────────────────────────────────
DELETED=$(find "$BACKUP_DIR" -name "*.sql.gz" -mtime +"$RETENTION_DAYS" -print -delete | wc -l)
echo "[$(date -Iseconds)] INFO  Retention cleanup: removed ${DELETED} old backup(s) (>${RETENTION_DAYS}d)"

# ── Offsite sync (Hetzner Storage Box via rclone) ────────────
# Setup once:
#   sudo apt install rclone -y
#   rclone config        # name=hetznerbox, type=sftp, host=u<id>.your-storagebox.de
#                        # user=u<id>, key_file=/home/deploy/.ssh/id_ed25519
# Configurable via env: RCLONE_REMOTE (default: hetznerbox:aykutonpc-backups)
RCLONE_REMOTE="${RCLONE_REMOTE:-hetznerbox:aykutonpc-backups}"
if command -v rclone >/dev/null 2>&1 && rclone listremotes | grep -q "^${RCLONE_REMOTE%%:*}:"; then
    echo "[$(date -Iseconds)] INFO  Syncing to ${RCLONE_REMOTE} ..."
    if rclone copy "$BACKUP_DIR" "$RCLONE_REMOTE" --min-age 1s --log-level INFO 2>&1; then
        echo "[$(date -Iseconds)] INFO  Offsite sync OK."
    else
        echo "[$(date -Iseconds)] WARN  Offsite sync failed (local backup retained)."
    fi
else
    echo "[$(date -Iseconds)] INFO  rclone not configured; skipping offsite sync."
fi

echo "[$(date -Iseconds)] INFO  Backup job finished successfully."
