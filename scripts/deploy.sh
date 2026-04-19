#!/usr/bin/env bash
# ============================================================
# AykutOnPC — Zero-Downtime Production Deploy Script
# Usage: bash deploy.sh [--branch main] [--skip-backup]
# Run as the 'deploy' user on the Hetzner VPS.
# ============================================================
set -euo pipefail

# ── Config ───────────────────────────────────────────────────
DEPLOY_DIR="/opt/aykutonpc"
COMPOSE_FILE="docker-compose.prod.yml"
ENV_FILE=".env.prod"
APP_CONTAINER="aykutonpc-web"
# Health check runs INSIDE the web container — the host has no 8080 port mapping
# (only nginx exposes 80/443), so a host-side `curl http://localhost:8080/health`
# always fails even when the app is perfectly healthy. `docker exec` hits the
# container on its own loopback, which is what the compose healthcheck and the
# nginx upstream both rely on.
HEALTH_CMD="docker exec $APP_CONTAINER curl -fsS http://localhost:8080/health"
HEALTH_RETRIES=15
HEALTH_INTERVAL=4
NGINX_CONTAINER="aykutonpc-nginx"
BRANCH="main"
SKIP_BACKUP=false
# Log to a path the deploy user actually owns. /var/log/ requires root, which
# CI'd ssh-action does not have, so the previous "/var/log/aykutonpc-deploy.log"
# choice failed at the very first log() call with "tee: Permission denied" and
# aborted the whole script (set -e + pipefail). Keeping logs alongside the
# repo also makes them grep-able by the deploy user without sudo.
LOG_FILE="${DEPLOY_DIR}/logs/deploy.log"
mkdir -p "$(dirname "$LOG_FILE")"

# ── Colours ──────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'

log()  { echo -e "${CYAN}[$(date -Iseconds)] $*${NC}" | tee -a "$LOG_FILE"; }
ok()   { echo -e "${GREEN}[$(date -Iseconds)] ✅ $*${NC}" | tee -a "$LOG_FILE"; }
warn() { echo -e "${YELLOW}[$(date -Iseconds)] ⚠️  $*${NC}" | tee -a "$LOG_FILE"; }
fail() { echo -e "${RED}[$(date -Iseconds)] ❌ $*${NC}" | tee -a "$LOG_FILE"; exit 1; }

# ── Parse args ────────────────────────────────────────────────
# Proper while/shift loop. The previous `for arg in $@; case --branch) BRANCH=${3:-main}`
# was broken: $3 reads the THIRD positional arg of the script, not the value
# AFTER --branch. So `deploy.sh --branch master --skip-backup` set
# BRANCH=--skip-backup and then `git checkout --skip-backup` exploded under
# `set -e`. Loop with explicit shift makes the value pairing unambiguous.
while [[ $# -gt 0 ]]; do
  case $1 in
    --skip-backup) SKIP_BACKUP=true; shift ;;
    --branch)      BRANCH="${2:-main}"; shift 2 ;;
    *)             shift ;;
  esac
done

# ── Guards ───────────────────────────────────────────────────
[[ -f "$DEPLOY_DIR/$ENV_FILE" ]] || fail "Missing $ENV_FILE. Aborting."
[[ -f "$DEPLOY_DIR/$COMPOSE_FILE" ]] || fail "Missing $COMPOSE_FILE. Aborting."

log "════════════════════════════════════════"
log " AykutOnPC Deploy — branch: $BRANCH"
log "════════════════════════════════════════"

cd "$DEPLOY_DIR"

# ── Step 1: Pre-deploy database backup ───────────────────────
if [[ "$SKIP_BACKUP" == false ]]; then
    log "[1/6] Running pre-deploy database backup..."
    if bash scripts/backup-db.sh; then
        ok "Pre-deploy backup complete."
    else
        warn "Backup failed. Proceeding anyway (backup is non-blocking)."
    fi
else
    warn "[1/6] Backup skipped (--skip-backup flag)."
fi

# ── Step 2: Pull latest code ─────────────────────────────────
log "[2/6] Pulling latest code from origin/$BRANCH..."
git fetch origin
git checkout "$BRANCH"
git reset --hard "origin/$BRANCH"
ok "Code updated to $(git rev-parse --short HEAD)."

# ── Step 3: Build new image ───────────────────────────────────
log "[3/6] Building new Docker image (no-cache for production)..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" \
    build --no-cache --pull web
ok "Image built successfully."

# ── Step 4: Record current container ID for rollback ─────────
PREVIOUS_IMAGE=$(docker inspect --format='{{.Image}}' "$APP_CONTAINER" 2>/dev/null || echo "none")
log "[4/6] Previous image recorded for rollback: ${PREVIOUS_IMAGE:0:20}..."

# ── Step 5: Rolling restart (web only — DB & Redis untouched) ─
log "[5/7] Rolling restart of web container..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" \
    up -d --no-deps web
ok "Web container restarted."

# ── Step 6: Reload nginx (config volume-mounted from repo) ────
# Nginx config is mounted via ./nginx/conf.d:/etc/nginx/conf.d:ro, so a `git
# reset --hard` in Step 2 already updated the file on disk. But nginx caches
# the parsed config in memory — `nginx -s reload` makes it pick up changes
# (CSP headers, rate limits, new locations) without dropping connections.
# If the config is invalid, reload fails gracefully and old config keeps running.
log "[6/7] Reloading nginx config..."
if docker exec "$NGINX_CONTAINER" nginx -t > /dev/null 2>&1; then
    docker exec "$NGINX_CONTAINER" nginx -s reload
    ok "Nginx config reloaded."
else
    warn "Nginx config test FAILED. Keeping old config running."
    docker exec "$NGINX_CONTAINER" nginx -t || true
fi

# ── Step 7: Health check gate ─────────────────────────────────
log "[7/7] Health check gate (${HEALTH_RETRIES} retries × ${HEALTH_INTERVAL}s)..."
ATTEMPT=0
until $HEALTH_CMD > /dev/null 2>&1; do
    ATTEMPT=$((ATTEMPT + 1))
    if [[ "$ATTEMPT" -ge "$HEALTH_RETRIES" ]]; then
        fail "Health check failed after $((ATTEMPT * HEALTH_INTERVAL))s. ROLLBACK REQUIRED."
        # Automatic rollback signal — Nginx keeps the old container running
        # until manual intervention. Alert here if you have a webhook.
    fi
    warn "  Health check attempt $ATTEMPT/$HEALTH_RETRIES — waiting ${HEALTH_INTERVAL}s..."
    sleep "$HEALTH_INTERVAL"
done

ok "Health check passed."

# ── Cleanup old dangling images ───────────────────────────────
log "Pruning dangling images..."
docker image prune -f --filter "until=24h" >> "$LOG_FILE" 2>&1 || true

COMMIT=$(git log -1 --pretty=format:'%h — %s (%an)')
ok "════════════════════════════════════════"
ok " Deploy complete."
ok " Commit : $COMMIT"
ok " Time   : $(date -Iseconds)"
ok "════════════════════════════════════════"
