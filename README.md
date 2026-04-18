# AykutOnPC

> Personal portfolio + AI chat + admin dashboard.
> .NET 9 · ASP.NET MVC · PostgreSQL 16 · Groq Llama 3.3 70B · Docker · Nginx · Hetzner.

[![CI](https://github.com/aykutcncik/AykutOnPC/actions/workflows/ci.yml/badge.svg)](.github/workflows/ci.yml)

---

## What's in here

A small but production-grade portfolio site with:

- **Cyberpunk-themed homepage** — hero, experience timeline, education, GitHub-fed "Latest Builds", skills, contact CTA.
- **AI chat widget** — `Microsoft.SemanticKernel` against Groq (OpenAI-compatible). Answers come from a knowledge-base table the admin can edit.
- **Admin dashboard** — JWT + cookie auth, BCrypt passwords, profile/experience/education/skills CRUD, **Visitor Analytics** dashboard (Chart.js, 30-day breakdown).
- **Visitor Intelligence middleware** — cookie-free, GDPR-safe page tracking. IPs are SHA-256 hashed with a daily-rotating salt.
- **Production stack** — multi-stage Docker build, healthchecks, resource limits, Nginx reverse proxy, Let's Encrypt, GitHub Actions CI/CD, daily PG backups with offsite rclone.

---

## Architecture

```
AykutOnPC.Core            # Domain entities, DTOs, configuration POCOs, interfaces.
AykutOnPC.Infrastructure  # EF Core 9 (Npgsql), services (Auth, Ai, GitHub, Profile, Analytics).
AykutOnPC.Web             # ASP.NET MVC controllers, views, middleware, DI wiring.
```

Three projects, no repository pattern (DbContext is the abstraction). Settings are bound via `IOptions<T>` from strongly-typed POCOs.

---

## Quick start (local Docker)

Prereqs: Docker Desktop, ~2 GB free RAM.

```bash
# 1. Clone
git clone https://github.com/aykutcncik/AykutOnPC.git
cd AykutOnPC

# 2. Create .env  (Groq key + admin password are mandatory; the rest have safe dev defaults)
cat > .env <<'ENV'
GEMINI_API_KEY=gsk_yourGroqKey
ADMIN_PASSWORD=ChangeMe!Admin
JWT_SECRET_KEY=please-generate-with-openssl-rand-base64-64
DB_PASSWORD=DevOnlyPassword2026!
ALLOWED_HOSTS=localhost
ENV

# 3. Bring up the stack (web + postgres)
docker compose up -d --build

# 4. Seed the admin user (first run only)
docker compose exec web dotnet AykutOnPC.Web.dll --seed-admin

# 5. Open
# http://localhost:8080            — public site
# http://localhost:8080/Admin      — login: aykut / <ADMIN_PASSWORD>
# http://localhost:8080/health     — JSON health report (db + redis checks)
```

Heads-up: in a fresh dev DB the **AI chat returns an empty answer** until you add `KnowledgeEntries`, and **Latest Builds is empty** unless `SeedData__GitHubUsername` is set and GitHub responds. Both are expected — the page renders gracefully (skeleton loader for builds, fallback message for chat).

---

## Running without Docker

```bash
# .NET 9 SDK + Postgres 15+ required.
dotnet user-secrets set "GeminiSettings:ApiKey" "gsk_yourGroqKey" --project AykutOnPC.Web
dotnet user-secrets set "SeedData:AdminUser:Password" "ChangeMe!Admin" --project AykutOnPC.Web

# Update appsettings.Development.json ConnectionStrings:DefaultConnection if your
# Postgres is not on localhost:5432.

dotnet ef database update --project AykutOnPC.Infrastructure --startup-project AykutOnPC.Web
dotnet run --project AykutOnPC.Web
dotnet run --project AykutOnPC.Web -- --seed-admin
```

---

## Configuration reference

All settings live under one of these sections (POCOs in `AykutOnPC.Core/Configuration`):

| Section            | Key                           | Purpose |
|--------------------|-------------------------------|---------|
| `JwtSettings`      | `Key`, `Issuer`, `Audience`   | JWT signing |
| `GeminiSettings`   | `ApiKey`, `ModelId`, `Endpoint`, `SystemPrompt` | Groq chat (name kept for backwards compat) |
| `GitHubSettings`   | `CacheMinutes`                | GitHub repo cache TTL |
| `SecuritySettings` | `DataProtectionPath`          | Where ASP.NET DP keys are persisted |
| `SeedData`         | `AdminUser.{Username,Email,Password}`, `GitHubUsername`, `HeroTitle`, `HeroSubtitle` | First-run seeding |

Everything can also be set via env vars (`__` separator), e.g. `GeminiSettings__ApiKey`, or via the flat aliases used in `docker-compose.yml`: `GEMINI_API_KEY`, `ADMIN_PASSWORD`, `JWT_SECRET_KEY`.

---

## Production deployment (Hetzner)

The full step-by-step guide is in **[docs/deployment/turkce-deploy-rehberi.md](docs/deployment/turkce-deploy-rehberi.md)** (Turkish).

Summary:

1. Order a Hetzner CX22 (Falkenstein) running Ubuntu 24.04 LTS.
2. SSH in as root → run [`scripts/harden-server.sh`](scripts/harden-server.sh) (creates `deploy` user, configures UFW + Fail2Ban + sshd, installs Docker).
3. Clone this repo to `/opt/aykutonpc`, create `.env.prod` with `openssl rand -base64 32` for every secret.
4. `docker compose -f docker-compose.prod.yml --env-file .env.prod up -d`.
5. `docker compose exec web dotnet AykutOnPC.Web.dll --seed-admin`.
6. Once a domain is live, run Certbot per the guide; Nginx auto-restarts after cert renew.
7. Add `HETZNER_HOST` + `HETZNER_SSH_KEY` to GitHub Secrets — pushes to `main` then auto-deploy via `scripts/deploy.sh` (with rollback on `/health` failure).

---

## Useful files

| Path                                      | Why you'd touch it |
|-------------------------------------------|--------------------|
| `Dockerfile`                              | Multi-stage build, non-root `appuser`, includes `curl` for healthcheck |
| `docker-compose.yml`                      | Local dev: web + postgres |
| `docker-compose.prod.yml`                 | Hetzner: + redis + nginx, healthchecks + resource limits |
| `nginx/nginx.conf`, `nginx/conf.d/`       | TLS 1.2+1.3, HSTS, rate limits, security headers |
| `scripts/harden-server.sh`                | One-shot Hetzner OS hardening |
| `scripts/deploy.sh`                       | Pre-backup → pull → build → rolling restart → /health poll → rollback on fail |
| `scripts/backup-db.sh`                    | Daily PG dump + 7-day retention + rclone offsite |
| `scripts/smoke-test.sh`                   | Production smoke tests against a deployed URL |
| `.github/workflows/ci.yml`                | dotnet test + Docker build + SSH deploy on `main` |

---

## Verification

```bash
# Health (returns JSON breakdown — db + redis checks)
curl http://localhost:8080/health | jq

# Chat (will return ApiNotConfigured if GEMINI_API_KEY is missing)
curl -X POST http://localhost:8080/api/chat/ask \
  -H "Content-Type: application/json" \
  -d '{"message":"Aykut kimdir?"}'

# Rate limit sanity (chat: 10/min, login: 5/min/IP)
for i in {1..15}; do curl -s -o /dev/null -w "%{http_code}\n" -X POST \
  http://localhost:8080/api/chat/ask -H "Content-Type: application/json" \
  -d '{"message":"hi"}'; done

# Production smoke test (after deploy)
bash scripts/smoke-test.sh https://aykutonpc.com
```

---

## License & contact

This is a personal project. Code is not licensed for redistribution.

Aykut Çinçik — [aykutcincik@ogr.eskisehir.edu.tr](mailto:aykutcincik@ogr.eskisehir.edu.tr)
