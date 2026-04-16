# Hetzner Production Deployment Guide — AykutOnPC

> Author: Enterprise Agent Team (DevOps + Cybersecurity) · v1.0 · 2026-04-16
> Scope: Fresh Hetzner VPS → Production traffic (Render + Supabase sunset)
> Total setup time: **~2 hours** (excluding DNS propagation)
> Monthly cost: **~€9.22** (CX22 + backups + Storage Box)

---

## 0. Pre-flight Checklist

Before you SSH into anything, have ready on your local machine:

- [ ] Hetzner Cloud account (https://accounts.hetzner.com/signUp)
- [ ] Payment method linked (credit card / PayPal / SEPA)
- [ ] SSH keypair (`~/.ssh/id_ed25519`) — generate with: `ssh-keygen -t ed25519 -C "aykutcincik@ogr.eskisehir.edu.tr"`
- [ ] **Fresh Groq API key** (old one was rotated — see Sprint 0)
- [ ] Supabase PostgreSQL connection string (for one-time data export)
- [ ] Current `.env` / appsettings local values for reference
- [ ] GitHub repository admin access (to add CI secrets later)

Optional but recommended:
- [ ] Domain name secured (e.g. via Namecheap, Cloudflare, or GoDaddy) — can be added post-launch
- [ ] Hetzner Storage Box BX11 (1 TB, €3.81/mo) for offsite backups

---

## 1. Provision the VPS

### 1.1 Create Project
- Hetzner Cloud console → **New Project** → name it `aykutonpc`

### 1.2 Add SSH Key
- Project → **Security → SSH Keys → Add SSH Key**
- Paste content of `~/.ssh/id_ed25519.pub`, name it (e.g., `Aykut-Laptop`)

### 1.3 Create Server
- **Location**: Falkenstein (FSN1) — EU, GDPR, lowest latency from Turkey
- **Image**: Ubuntu 24.04 LTS
- **Type**: **CX22** (2 vCPU AMD, 4 GB RAM, 40 GB NVMe, 20 TB traffic) — €4.51/mo
- **Networking**: IPv4 + IPv6 (default)
- **SSH Key**: select the one just added
- **Volume**: none (NVMe is sufficient)
- **Firewall**: skip (we'll use UFW from `harden-server.sh`)
- **Backups**: enable (Hetzner daily snapshots, +20% = €0.90/mo)
- **Cloud config (user data)** — paste this to bootstrap Docker before first SSH:
  ```yaml
  #cloud-config
  package_update: true
  package_upgrade: true
  packages:
    - git
    - curl
    - ufw
    - fail2ban
    - unattended-upgrades
    - auditd
  runcmd:
    - loadkeys us
    - timedatectl set-timezone Europe/Istanbul
  ```
- **Hostname**: `aykutonpc-prod-01`
- Click **Create & Buy now**

### 1.4 Note the Public IP
- After ~30s, server is ready. Copy the IPv4 address (e.g., `49.12.XX.XX`)

---

## 2. First SSH + Server Hardening

### 2.1 Connect
```bash
ssh root@<HETZNER_IP>
# First connection prompts to verify fingerprint — type 'yes'
```

### 2.2 Run Hardening Script
The repo already ships [scripts/harden-server.sh](../../scripts/harden-server.sh). Pull it via git clone:

```bash
cd /opt
git clone https://github.com/<YOUR_GITHUB_USER>/AykutOnPC.git aykutonpc
cd aykutonpc
bash scripts/harden-server.sh
```

The script performs:
- Creates `deploy` user with sudo + docker group
- Copies your SSH authorized_keys from root to `deploy`
- Disables root SSH login, password auth, sets MaxAuthTries=3, LoginGraceTime=30
- Installs Docker CE + docker-compose-plugin from official apt repo
- UFW: default deny incoming, allows 22/80/443 only, enables
- Fail2Ban: SSH filter, 3 failures in 600s = 1h ban
- Sysctl kernel hardening (SYN cookies, rp_filter, ICMP redirects off)

### 2.3 Re-login as `deploy`
```bash
exit   # leave root session
ssh deploy@<HETZNER_IP>
sudo whoami   # → root (confirms sudo works)
```

### 2.4 Verify Hardening
```bash
sudo ufw status verbose
# → Default: deny (incoming), allow (outgoing); 22,80,443/tcp ALLOW
sudo systemctl status fail2ban   # → active (running)
sudo docker --version            # → Docker version 27.x.x
sudo docker compose version      # → Docker Compose version v2.x.x
# Confirm SSH root login is disabled
sudo grep -E "^PermitRootLogin|^PasswordAuthentication" /etc/ssh/sshd_config
# → PermitRootLogin no / PasswordAuthentication no
```

---

## 3. Application Deployment

### 3.1 Fix Ownership
```bash
sudo chown -R deploy:deploy /opt/aykutonpc
cd /opt/aykutonpc
```

### 3.2 Create Production Environment File
```bash
cp .env.prod.example .env.prod
chmod 600 .env.prod   # lock it down
nano .env.prod        # or vim
```

Generate all secrets with `openssl rand`:
```bash
# Run these on the server, paste output into .env.prod
openssl rand -base64 32   # → DB_PASSWORD
openssl rand -base64 32   # → REDIS_PASSWORD
openssl rand -base64 64   # → JWT_SECRET_KEY
openssl rand -base64 24   # → ADMIN_PASSWORD (you'll use this to login!)
```

Populate `.env.prod`:
```
DOMAIN_NAME=<your-domain-or-hetzner-ip>
DB_PASSWORD=<generated>
REDIS_PASSWORD=<generated>
JWT_SECRET_KEY=<generated>
ADMIN_PASSWORD=<generated-or-your-choice>
GEMINI_API_KEY=<fresh Groq key from Groq panel>
ASPNETCORE_ALLOWED_HOSTS=<your-domain-or-hetzner-ip>
```

**Store ADMIN_PASSWORD in a password manager — you'll need it to login.**

### 3.3 Prepare Directories
```bash
sudo mkdir -p /etc/letsencrypt/live/temp
sudo mkdir -p /opt/aykutonpc/backups
sudo chown -R deploy:deploy /opt/aykutonpc/backups
```

### 3.4 (Domain not ready yet) Generate Self-Signed Cert
Skip this section if you already have a domain pointed at the server.

```bash
sudo openssl req -x509 -nodes -days 30 -newkey rsa:2048 \
  -keyout /etc/letsencrypt/live/temp/privkey.pem \
  -out /etc/letsencrypt/live/temp/fullchain.pem \
  -subj "/C=TR/ST=Eskisehir/L=Eskisehir/O=AykutOnPC/CN=$(curl -s ifconfig.me)"
```

Update [nginx/conf.d/aykutonpc.conf](../../nginx/conf.d/aykutonpc.conf) to use the `temp/` certificate paths (or duplicate for now).

### 3.5 Start the Stack
```bash
cd /opt/aykutonpc
docker compose -f docker-compose.prod.yml --env-file .env.prod pull
docker compose -f docker-compose.prod.yml --env-file .env.prod build --no-cache web
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d
```

### 3.6 Monitor Startup
```bash
docker compose -f docker-compose.prod.yml logs -f web
# Watch for: "Now listening on: http://[::]:8080"
# Migration logs: "Background: Migrations applied successfully."
# If migrations fail 5x, check DB_PASSWORD + Host=db resolution
```

### 3.7 Seed the Admin User
```bash
docker compose -f docker-compose.prod.yml exec web \
  dotnet AykutOnPC.Web.dll --seed-admin
# Expected log: "Admin 'aykut' created successfully with Admin role."
```

### 3.8 Smoke Test
```bash
curl -kI https://localhost/health   # → 200 OK (via Nginx)
curl -kI https://<HETZNER_IP>       # → 200 (browser cert warning if self-signed)
```

---

## 4. Migrating Data from Supabase

Do this **once** during the cutover window. Plan ~15 minutes of read-only time on Supabase.

### 4.1 Dump from Supabase (run locally or from Hetzner)
```bash
# Install pg client if not present
sudo apt install postgresql-client-16 -y

export SUPABASE_URL="postgres://postgres:[YOUR_SUPABASE_PWD]@db.[PROJECT_REF].supabase.co:5432/postgres"

pg_dump "$SUPABASE_URL" \
  --schema=public \
  --no-owner \
  --no-acl \
  --clean \
  --if-exists \
  --exclude-schema=auth \
  --exclude-schema=storage \
  --exclude-schema=graphql \
  --exclude-schema=graphql_public \
  --exclude-schema=realtime \
  --exclude-schema=supabase_functions \
  -f /tmp/supabase_export.sql
```

### 4.2 Transfer to Hetzner (if dumped locally)
```bash
scp /tmp/supabase_export.sql deploy@<HETZNER_IP>:/tmp/
```

### 4.3 Restore
```bash
# On Hetzner
# Stop web container temporarily (so EF Core doesn't race)
docker compose -f docker-compose.prod.yml stop web

# Copy dump into db container
docker compose -f docker-compose.prod.yml cp /tmp/supabase_export.sql db:/tmp/

# Restore
docker compose -f docker-compose.prod.yml exec db \
  psql -U postgres -d AykutOnPC_Db -f /tmp/supabase_export.sql

# Verify row counts
docker compose -f docker-compose.prod.yml exec db \
  psql -U postgres -d AykutOnPC_Db -c \
  'SELECT schemaname,relname,n_live_tup FROM pg_stat_user_tables ORDER BY n_live_tup DESC;'

# Restart web
docker compose -f docker-compose.prod.yml start web
```

### 4.4 Post-restore Sanity
```bash
# Admin user present?
docker compose -f docker-compose.prod.yml exec db \
  psql -U postgres -d AykutOnPC_Db -c 'SELECT "Username","Role" FROM "Users";'

# Login via browser/curl
curl -k -c /tmp/c.txt -X POST https://<HETZNER_IP>/Account/Login \
  -d "Username=aykut&Password=<ADMIN_PASSWORD>" \
  -H "Content-Type: application/x-www-form-urlencoded"
```

---

## 5. Domain + Let's Encrypt (Once DNS Ready)

### 5.1 Point DNS
At your registrar, create:
- `A` record: `@` → `<HETZNER_IP>`, TTL 300
- `A` record: `www` → `<HETZNER_IP>`, TTL 300

Verify:
```bash
dig +short <YOUR_DOMAIN>
# → <HETZNER_IP>
```

### 5.2 Obtain Real Certificate
```bash
sudo apt install certbot -y

# Stop nginx container briefly (port 80 must be free for HTTP-01 challenge)
docker compose -f docker-compose.prod.yml stop nginx

sudo certbot certonly --standalone \
  -d <YOUR_DOMAIN> -d www.<YOUR_DOMAIN> \
  --email aykutcincik@ogr.eskisehir.edu.tr \
  --agree-tos --no-eff-email \
  --rsa-key-size 4096

# Certificate installed at:
# /etc/letsencrypt/live/<YOUR_DOMAIN>/fullchain.pem
# /etc/letsencrypt/live/<YOUR_DOMAIN>/privkey.pem
```

### 5.3 Update Nginx Config
Edit [nginx/conf.d/aykutonpc.conf](../../nginx/conf.d/aykutonpc.conf):
- Replace `server_name` directives with `<YOUR_DOMAIN> www.<YOUR_DOMAIN>`
- Replace ssl_certificate paths from `temp/` to `<YOUR_DOMAIN>/`

Also update `.env.prod`:
```
DOMAIN_NAME=<YOUR_DOMAIN>
ASPNETCORE_ALLOWED_HOSTS=<YOUR_DOMAIN>;www.<YOUR_DOMAIN>
```

Restart nginx + web:
```bash
docker compose -f docker-compose.prod.yml up -d --force-recreate nginx web
```

### 5.4 Automate Renewal
```bash
sudo crontab -e
```
Add:
```
0 3 * * * certbot renew --quiet --deploy-hook "cd /opt/aykutonpc && docker compose -f docker-compose.prod.yml exec nginx nginx -s reload"
```

### 5.5 Verify A+ SSL Grade
Browser: https://www.ssllabs.com/ssltest/analyze.html?d=<YOUR_DOMAIN>
- Target: **A+** (TLS 1.2+1.3, modern ciphers, HSTS preload, OCSP stapling — all configured in `nginx.conf`)

---

## 6. CI/CD — GitHub Actions

The pipeline [.github/workflows/ci.yml](../../.github/workflows/ci.yml) is already wired.

### 6.1 Add Secrets
In GitHub repo → **Settings → Secrets and variables → Actions → New repository secret**:

| Name | Value |
|---|---|
| `HETZNER_HOST` | Hetzner public IP or domain |
| `HETZNER_SSH_KEY` | Contents of your **private** SSH key that was added to the server's `~/.ssh/authorized_keys` |

### 6.2 First Automated Deploy
Push any change to `main` → Actions tab → watch:
1. Build + test
2. Docker image build validation
3. SSH into Hetzner + run `scripts/deploy.sh`

[scripts/deploy.sh](../../scripts/deploy.sh) performs:
- Pre-deploy `pg_dump` backup
- `git fetch + reset --hard origin/main`
- Record previous image for rollback
- Build new image
- Rolling restart of `web` only (DB/Redis untouched)
- 15 health-check retries × 4s = 60s gate
- Rollback on failure

Log: `/var/log/aykutonpc-deploy.log`

---

## 7. Backups

### 7.1 Local Daily Backup
Already implemented in [scripts/backup-db.sh](../../scripts/backup-db.sh). Cron it:
```bash
sudo crontab -e
```
Add:
```
0 3 * * * cd /opt/aykutonpc && bash scripts/backup-db.sh >> /var/log/aykutonpc-backup.log 2>&1
```

### 7.2 Offsite via Hetzner Storage Box (Recommended)
- Order Storage Box BX11 (1 TB, €3.81/mo)
- Get SFTP credentials from Hetzner Robot
- Install rclone:
  ```bash
  sudo apt install rclone -y
  rclone config   # type: sftp, host: u12345.your-storagebox.de, user: u12345, key_file
  ```
- Uncomment rclone block in `scripts/backup-db.sh` (target: `hetznerbox:aykutonpc-backups/`)

### 7.3 Test Restore (Monthly Drill)
```bash
# Pick the latest backup
LATEST=$(ls -t /opt/aykutonpc/backups/*.sql.gz | head -1)
# Restore into a throwaway database
gunzip -c "$LATEST" | docker compose -f docker-compose.prod.yml exec -T db \
  psql -U postgres -d AykutOnPC_Db_RestoreTest
# Verify row counts match prod
# Drop the test DB
```

---

## 8. Post-Deploy Checklist

- [ ] `/health` returns 200
- [ ] HTTPS grade A+ on SSL Labs
- [ ] Admin login works (cookies set, redirects to `/admin`)
- [ ] Chat API returns AI responses (Turkish)
- [ ] GitHub builds show on portfolio page (cache populated in 10 min)
- [ ] Visitor analytics dashboard shows PageView counts growing
- [ ] Fail2Ban log shows bans accumulating (normal for any public IP)
- [ ] First automated backup executed at 03:00 UTC next day
- [ ] First CI/CD deploy succeeded (check Actions logs)
- [ ] Render service paused / scaled to zero
- [ ] Supabase final backup archived

---

## 9. Cost Breakdown

| Line item | Monthly |
|---|---|
| Hetzner CX22 (2 vCPU / 4 GB / 40 GB NVMe) | €4.51 |
| Hetzner daily snapshots (+20%) | €0.90 |
| Storage Box BX11 (1 TB SFTP) | €3.81 |
| Domain (amortized, varies) | €1.00 |
| **Total** | **~€10.22** |

vs. previous (Render Starter + Supabase Pro): ~$32/mo → savings **~€20/mo**, much more control.

---

## 10. Troubleshooting

### `502 Bad Gateway` from Nginx
- Container likely not healthy. Check: `docker compose logs web`
- Health endpoint: `docker compose exec web curl -sf http://localhost:8080/health`
- DB connection: `docker compose exec db pg_isready -U postgres`

### Migration loops forever
- Check `.env.prod` `DB_PASSWORD` matches `POSTGRES_PASSWORD` in compose
- `docker compose logs db` for PostgreSQL auth errors

### Let's Encrypt rate limited
- Test with staging: append `--staging` to certbot command first
- Rate limit: 5 certs per week per registered domain

### Data Protection 500 errors on Login
- `/app/keys` volume must persist. Check: `docker volume inspect aykutonpc_app-keys`
- Inside container: `ls /app/keys` should show `.xml` files after first request

### High memory on CX22
- `docker stats` — web usually ~250 MB, db ~150 MB, redis ~80 MB, nginx ~10 MB
- If db spikes: tune `postgres/postgresql.conf` work_mem downward

### SSH locked out (Fail2Ban)
- From another location with Hetzner Console (rescue): `sudo fail2ban-client set sshd unbanip <YOUR_IP>`

---

## 11. Security Audit Checklist

Run periodically (monthly):

- [ ] `sudo apt list --upgradable` shows 0 security updates (unattended-upgrades handles these)
- [ ] `sudo fail2ban-client status sshd` → banned IPs
- [ ] `sudo ufw status numbered` → only 22/80/443 ALLOW
- [ ] `ss -tlnp` on host → 22 (sshd), 80+443 (docker-proxy), nothing else
- [ ] SSL Labs re-scan → still A+
- [ ] `.env.prod` file mode 600, owner deploy
- [ ] `git log --all --full-history -- AykutOnPC.Web/appsettings.Development.json` → no API keys in diffs
- [ ] Last successful backup file timestamp (should be <26h old)
- [ ] Log rotation working: `sudo journalctl --disk-usage` not exploding

---

## 12. Decommissioning Render + Supabase

Wait **1 full week** after Hetzner go-live to observe stability.

### Render
- Dashboard → service → **Suspend** (keeps data), verify nothing breaks
- After 1 more week → **Delete Service**

### Supabase
- Take final `pg_dump` → archive to Storage Box
- Disable database via API keys (paranoid mode)
- Downgrade plan to Free tier (won't incur costs but keeps access if rollback needed)
- After 30 days → delete project

---

## 13. References

- Hetzner Cloud API: https://docs.hetzner.cloud/
- Docker Compose v2 docs: https://docs.docker.com/compose/
- Nginx security headers: https://nginx.org/en/docs/
- Let's Encrypt certbot: https://eff-certbot.readthedocs.io/
- EF Core 9 migrations: https://learn.microsoft.com/ef/core/managing-schemas/migrations/
- OWASP deployment cheatsheet: https://cheatsheetseries.owasp.org/cheatsheets/Deployment_Cheat_Sheet.html

---

_End of guide · For incident response, see [runbook.md](./runbook.md)_
