#!/usr/bin/env bash
#
# Prune Visitor Intelligence (PageViews) and ChatLogs older than retention window.
# Runs daily via cron. Idempotent — safe to re-run.
#
# Retention windows (override via env):
#   PAGEVIEW_RETENTION_DAYS — default 30
#   CHATLOG_RETENTION_DAYS  — default 90 (chat memory + admin review)
#
# Logs to /var/log/aykutonpc-prune.log
# Exit codes: 0 ok, 1 db unreachable, 2 sql error
#
set -euo pipefail

LOG=/var/log/aykutonpc-prune.log
PV_DAYS="${PAGEVIEW_RETENTION_DAYS:-30}"
CL_DAYS="${CHATLOG_RETENTION_DAYS:-90}"
DB_CONTAINER="aykutonpc-db"
DB_USER="postgres"
DB_NAME="AykutOnPC_Db"

ts() { date -u +"%Y-%m-%dT%H:%M:%SZ"; }
log() { echo "[$(ts)] $*" | tee -a "$LOG"; }

log "Prune start — PV>${PV_DAYS}d, CL>${CL_DAYS}d"

if ! docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" -q; then
    log "ERROR: DB not ready"
    exit 1
fi

PV_DELETED=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -A -c \
    "DELETE FROM \"PageViews\" WHERE \"VisitedAtUtc\" < NOW() - INTERVAL '${PV_DAYS} days' RETURNING 1;" \
    2>>"$LOG" | wc -l) || { log "ERROR: PageViews delete failed"; exit 2; }

CL_DELETED=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -A -c \
    "DELETE FROM \"ChatLogs\" WHERE \"CreatedAtUtc\" < NOW() - INTERVAL '${CL_DAYS} days' RETURNING 1;" \
    2>>"$LOG" | wc -l) || { log "ERROR: ChatLogs delete failed"; exit 2; }

# Reclaim space (autovacuum eventually does this; explicit nudge for big deletes)
if [ "$PV_DELETED" -gt 100 ] || [ "$CL_DELETED" -gt 100 ]; then
    docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -c \
        "VACUUM (ANALYZE) \"PageViews\", \"ChatLogs\";" >>"$LOG" 2>&1 || true
fi

log "Prune done — PageViews removed=${PV_DELETED}, ChatLogs removed=${CL_DELETED}"
