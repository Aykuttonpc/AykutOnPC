# Operational Runbook — AykutOnPC Production

> Audience: On-call (you) · Server: Hetzner CX22 · Stack: Docker Compose
> Sister doc: [hetzner-production-guide.md](./hetzner-production-guide.md)
> Quick contacts:
> - Hetzner Console: https://console.hetzner.cloud (rescue/recovery boot)
> - Groq panel: https://console.groq.com (AI key rotation)
> - Domain registrar: <fill in once domain is bought>

---

## 0. First Things to Run on Login

Every time you SSH in for an investigation, get the lay of the land:

```bash
ssh deploy@<HETZNER_HOST>

cd /opt/aykutonpc
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs --tail=50
df -h          # disk usage
free -h        # memory
uptime         # load average + uptime
sudo systemctl status fail2ban
```

Healthy snapshot:
- All 4 containers `Up (healthy)`
- Disk `/` at <80% used
- Memory: ~1.5 GB used / 4 GB
- Load average <2.0
- Fail2Ban active

---

## 1. Common Incidents

### 1.1 Site is Down (5xx, timeout)

```bash
# Step 1: Are containers up?
docker compose -f docker-compose.prod.yml ps
# If 'web' is exited / unhealthy → next step
# If all healthy → check Nginx / SSL → section 1.5

# Step 2: Web container logs
docker compose -f docker-compose.prod.yml logs --tail=200 web | less
# Look for: stack traces, "Migration failed", "Cannot connect to db"

# Step 3: Force restart web only
docker compose -f docker-compose.prod.yml restart web
sleep 30
curl -kI https://localhost/health
```

If still down → **Section 4 (Rollback)**

### 1.2 Database is Down

```bash
docker compose -f docker-compose.prod.yml exec db pg_isready -U postgres
# If not ready:
docker compose -f docker-compose.prod.yml logs --tail=200 db
# Common: out-of-disk, corruption, OOM kill
```

If corrupted → **Section 3 (Restore from backup)**

### 1.3 Out of Disk

```bash
df -h
sudo du -sh /var/lib/docker/* | sort -rh | head -10
# Likely culprit: docker images
docker image prune -a --filter "until=24h"
docker system prune --volumes  # WARNING: removes unused volumes too — verify before!
sudo journalctl --vacuum-time=7d
```

### 1.4 High CPU / Slow

```bash
docker stats --no-stream
# Check which container is hot
top -p $(pgrep -d, -f dotnet)   # thread-level if web is hot
# If db is hot:
docker compose -f docker-compose.prod.yml exec db \
  psql -U postgres -d AykutOnPC_Db -c \
  "SELECT pid, now()-query_start AS dur, state, query FROM pg_stat_activity WHERE state='active' ORDER BY dur DESC LIMIT 10;"
```

### 1.5 SSL / TLS Issues

```bash
# Check cert expiry
echo | openssl s_client -servername <DOMAIN> -connect <DOMAIN>:443 2>/dev/null \
  | openssl x509 -noout -dates
# Force renewal
sudo certbot renew --force-renewal
docker compose -f docker-compose.prod.yml exec nginx nginx -s reload
```

### 1.6 SSH Locked Out (Fail2Ban banned you)

Use Hetzner Console (web-based VNC):
1. Hetzner Cloud → Server → **Console** button
2. Login as `deploy` (password set via `passwd deploy` post-harden, or use cloud-init root rescue)
3. Unban your IP:
   ```bash
   sudo fail2ban-client set sshd unbanip <YOUR_IP>
   ```

---

## 2. Standard Operations

### 2.1 Manual Deploy
```bash
cd /opt/aykutonpc
bash scripts/deploy.sh --branch main
tail -f /var/log/aykutonpc-deploy.log
```

### 2.2 View Logs (live)
```bash
# All containers
docker compose -f docker-compose.prod.yml logs -f --tail=100
# Just web
docker compose -f docker-compose.prod.yml logs -f web
# Just nginx access log (JSON)
docker compose -f docker-compose.prod.yml logs -f nginx | jq -c 'select(.status >= 400)'
```

### 2.3 Run a Migration Manually
```bash
docker compose -f docker-compose.prod.yml exec web \
  dotnet ef database update --project AykutOnPC.Infrastructure
# Or, if EF tools not in image, force the startup migration loop by restarting web:
docker compose -f docker-compose.prod.yml restart web
```

### 2.4 Re-seed Admin (forgot password / reset)
```bash
# Update ADMIN_PASSWORD in .env.prod first, then:
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate web
docker compose -f docker-compose.prod.yml exec web \
  dotnet AykutOnPC.Web.dll --seed-admin
# This will re-hash & overwrite the existing admin's password.
```

### 2.5 Open a psql Shell
```bash
docker compose -f docker-compose.prod.yml exec db \
  psql -U postgres -d AykutOnPC_Db
```

### 2.6 Open a Redis CLI
```bash
docker compose -f docker-compose.prod.yml exec redis \
  redis-cli -a "$REDIS_PASSWORD"
```

### 2.7 Inspect a Backup
```bash
ls -lh /opt/aykutonpc/backups/
# Decompress and peek
gunzip -c /opt/aykutonpc/backups/<file>.sql.gz | head -50
```

---

## 3. Restore from Backup

### 3.1 Choose the Backup
```bash
ls -t /opt/aykutonpc/backups/*.sql.gz | head -10
# Or pull from Storage Box if local backup is corrupted
rclone copy hetznerbox:aykutonpc-backups/<file> /tmp/
```

### 3.2 Stop the App (prevent writes during restore)
```bash
docker compose -f docker-compose.prod.yml stop web
```

### 3.3 Drop & Recreate the Database
```bash
docker compose -f docker-compose.prod.yml exec db psql -U postgres -c \
  'DROP DATABASE "AykutOnPC_Db" WITH (FORCE);'
docker compose -f docker-compose.prod.yml exec db psql -U postgres -c \
  'CREATE DATABASE "AykutOnPC_Db";'
```

### 3.4 Restore
```bash
gunzip -c /opt/aykutonpc/backups/<file>.sql.gz | \
  docker compose -f docker-compose.prod.yml exec -T db \
  psql -U postgres -d AykutOnPC_Db
```

### 3.5 Verify and Restart
```bash
docker compose -f docker-compose.prod.yml exec db psql -U postgres -d AykutOnPC_Db -c \
  'SELECT count(*) FROM "Users"; SELECT count(*) FROM "PageViews";'
docker compose -f docker-compose.prod.yml start web
sleep 30
curl -kI https://localhost/health
```

---

## 4. Rollback a Bad Deploy

### 4.1 Automated (deploy.sh failed mid-flight)
`scripts/deploy.sh` records the previous image tag. Read its log:
```bash
tail -100 /var/log/aykutonpc-deploy.log
# Find: "Saved previous image: aykutonpc-web:<sha>"
PREV_IMAGE="aykutonpc-web:<sha>"
docker tag "$PREV_IMAGE" aykutonpc-web:latest
docker compose -f docker-compose.prod.yml up -d --force-recreate web
```

### 4.2 Manual (deploy succeeded but bug in production)
```bash
cd /opt/aykutonpc
git log --oneline -10
git checkout <previous-good-sha>
docker compose -f docker-compose.prod.yml build --no-cache web
docker compose -f docker-compose.prod.yml up -d --force-recreate web
# Once stable, fix forward in main branch
git checkout main
```

---

## 5. Secret Rotation Procedures

### 5.1 Rotate Groq API Key
1. Groq panel → API Keys → revoke old, create new
2. On Hetzner: edit `.env.prod` → update `GEMINI_API_KEY`
3. `docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate web`

### 5.2 Rotate JWT Secret
**Warning**: All existing user sessions will be invalidated (forced re-login).
```bash
NEW=$(openssl rand -base64 64)
sed -i "s|^JWT_SECRET_KEY=.*|JWT_SECRET_KEY=$NEW|" .env.prod
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate web
```

### 5.3 Rotate DB Password
**Warning**: Multi-step, app will be briefly down.
```bash
# 1. New password
NEW=$(openssl rand -base64 32)
# 2. Change inside Postgres
docker compose -f docker-compose.prod.yml exec db psql -U postgres -c \
  "ALTER USER postgres WITH PASSWORD '$NEW';"
# 3. Update .env.prod
sed -i "s|^DB_PASSWORD=.*|DB_PASSWORD=$NEW|" .env.prod
# 4. Restart web (db doesn't need restart)
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --force-recreate web
```

### 5.4 Rotate Admin Password
See **2.4 Re-seed Admin**.

---

## 6. Health Checks & SLOs

| Metric | Target | How to check |
|---|---|---|
| Uptime (monthly) | ≥99.5% | manual via UptimeRobot if configured, or grep nginx logs |
| `/health` p95 latency | <100 ms | `curl -w '%{time_total}\n' -o /dev/null -s https://<DOMAIN>/health` |
| First chat response | <5 s | `time curl -X POST .../api/chat ...` |
| Migration success rate | 100% | `docker compose logs web \| grep -i migration` |
| Backup recency | <26 hours | `find /opt/aykutonpc/backups -mtime -1 \| wc -l` ≥1 |
| SSL cert validity | >30 days | section 1.5 |
| Fail2Ban active | Yes | `sudo systemctl is-active fail2ban` |

---

## 7. Decision Trees

### "Should I rollback or fix forward?"
```
Is the bug data-corrupting?  → ROLLBACK + restore from backup
Is the bug user-facing 5xx?  → ROLLBACK to previous image
Is the bug cosmetic?         → Fix forward in next deploy
Is auth broken?              → ROLLBACK
Is /health failing?          → ROLLBACK (deploy.sh probably already did)
```

### "Should I scale up the VPS?"
```
load_avg consistently >3.0 for 1 week  → upgrade to CX32 (4 vCPU / 8 GB, ~€7.40/mo)
RAM consistently >85%                  → upgrade to CX32
Disk >80% with no cleanup possible     → add Hetzner volume (€0.04/GB/mo)
PageViews >1M/month                    → consider read replica + Redis cache
```

---

## 8. Disaster Recovery Tier Map

| Scenario | RPO | RTO | Procedure |
|---|---|---|---|
| Single container crash | 0 | <1 min | Auto-restart by Docker |
| Bad deploy (auto-detected) | 0 | <2 min | `deploy.sh` rollback (Section 4.1) |
| Bad deploy (manual) | 0 | <10 min | Section 4.2 |
| DB corruption | <24 h | <30 min | Section 3 |
| Server compromised | <24 h | <2 hours | New server (Section "Hetzner guide §1-3") + restore (§3) |
| Hetzner DC down | <24 h | <4 hours | New VPS in different DC + restore |
| Total data loss (worst case) | <24 h | <8 hours | Storage Box → new server → restore |

---

## 9. Useful One-liners

```bash
# Top 10 slowest requests in last hour (nginx access log)
docker compose -f docker-compose.prod.yml logs --since 1h nginx \
  | jq -s -c 'map(select(.request_time != null)) | sort_by(.request_time | tonumber) | reverse | .[:10]'

# 4xx/5xx count last 24h
docker compose -f docker-compose.prod.yml logs --since 24h nginx \
  | jq -c 'select(.status >= 400) | .status' | sort | uniq -c | sort -rn

# Unique visitors today (from PageViews)
docker compose -f docker-compose.prod.yml exec db psql -U postgres -d AykutOnPC_Db -c \
  "SELECT count(DISTINCT \"HashedIp\") FROM \"PageViews\" WHERE \"VisitedAtUtc\" >= current_date;"

# Top 10 banned IPs today
sudo fail2ban-client status sshd | grep -i banned

# Memory hog check
docker stats --no-stream --format 'table {{.Name}}\t{{.MemUsage}}\t{{.CPUPerc}}'
```

---

_Keep this runbook current — every incident teaches something. Add new sections as you learn._
