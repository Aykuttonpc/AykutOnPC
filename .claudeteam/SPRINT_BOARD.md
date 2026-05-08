# Sprint Board

> Aktif sprint. WIP'i bir kişide max 1-2 ile sınırla.
> Tarihler: ISO format (YYYY-MM-DD).

## Aktif Sprint

- **Sprint:** #3 — Blog Modülü
- **Başlangıç:** 2026-05-07
- **Hedef bitiş:** 2026-05-21 (1.5-2 hafta)
- **Sprint hedefi:** Markdown tabanlı blog: admin CRUD + public listing/detail + RSS + SEO. KnowledgeBase pattern'ini takip et — sapma yok.

---

## 📋 Todo

| ID | Başlık | Sahip | Tahmin | Notlar |
|---|---|---|---|---|
| T-#3-003 | `IBlogPostService` + `BlogPostService` (Infrastructure) | Senior Dev #1 | 1h | Caching: kısa TTL public listing için (10dk gibi KB pattern'i) |
| T-#3-004 | Admin `BlogController` + Razor view (CRUD) | Senior Dev #1 + #3 | 3h | KnowledgeBase admin layout'u kopyala, markdown editor (textarea + preview) |
| T-#3-005 | Public `BlogController` (listing `/blog`, detail `/blog/{slug}`) | Senior Dev #1 + #3 | 2h | Pagination yok (kişisel blog, az post). Tags filtreleme MVP dışı. |
| T-#3-006 | Markdown rendering — Markdig + HtmlSanitizer | Senior Dev #3 + AppSec | 1.5h | XSS savunması — admin trust'lansa bile defansif sanitize. NuGet: `Markdig`, `HtmlSanitizer`. |
| T-#3-007 | SEO: `<meta>` (title, description, OG, Twitter Card) + `sitemap.xml` entry | Senior Dev #3 | 1h | Sitemap blog post'lara `<url>` ekler |
| T-#3-008 | RSS feed `/blog/feed.xml` | Senior Dev #1 | 1h | `application/rss+xml`, son 20 post |
| T-#3-009 | ChatLogs/Profile/KB ile schema overlap kontrolü + index sanity check | Senior Dev #2 | 30dk | EXPLAIN ANALYZE listing query |
| T-#3-010 | Manuel test path: yarat → publish → public list → detail → XSS attempt → RSS validate | QA + Aykut | 30dk | Production smoke + iframe/script tag XSS test |
| T-D-009 | `Pgvector.EntityFrameworkCore` package'ı POC için Trial — Sprint #4'e bırakıldı | Innovation Architect | — | Brief yazıldı, implementation Sprint #4 |

## 🚧 In Progress

| ID | Başlık | Sahip | Başlangıç | Notlar |
|---|---|---|---|---|

## 👀 Review

| ID | Başlık | Sahip | PR/Branch | Bekleyen |
|---|---|---|---|---|

## ✅ Done

### Sprint #3 (devam ediyor)

| ID | Başlık | Tamamlanan | Notlar |
|---|---|---|---|
| T-#3-011 | Bot regex sıkılaştırma | 2026-05-08 | `VisitorTrackingMiddleware.BotPattern`: 20+ scanner family eklendi (zgrab, Censys, LeakIX, Modat, VisionHeight, Nuclei, Nikto, WPScan, sqlmap, masscan, httpx, Netcraft, Qualys, Shodan, Expanse, FOFA, …). Commit `6808377`. UA/IP breakdown analiz Sprint #4'e ertelendi (RAG ile çakışmasın). |
| T-#3-013 | VI veri retention policy + cron | 2026-05-08 | `scripts/prune-pageviews.sh` (PV=30g, CL=90g, env var override, auto-VACUUM). `ARCHITECTURE.md` "Veri Retention Policy" bölümü. Crontab production'da kurulu. |
| T-#3-012 | VPS runbook "IP ban recovery" bölümü eklendi | 2026-05-08 | `D:\AYKUTONPC-VPS-REHBERI.md` line 119+ — Adım 5 sonrası yeni "🚨 ACİL KURTARMA — IP-spesifik nft DROP" bölümü (Belirti / Sebep / Tanı / Recovery / Önlem). RUNBOOKS index 2026-05-08'e güncel. |
| 🚨 INC-001 | **Production lockout recovery + VI investigation** | 2026-05-08 | SSH 22 timeout (içerde nft IP-DROP + fail2ban kalıcı DB ban) → Web Console + Rescue mode + `nft flush ruleset` + `rm fail2ban.sqlite3` ile recovery. Detay: ADR-010. 8-sorgu VI tanı: 124 kayıt 2 günlük, 5 bot regex kaçağı, 42-visit aynı UA, admin bypass ✅. |

### Sprint #2 (kapatıldı 2026-05-07)

| ID | Başlık | Tamamlanan | Notlar |
|---|---|---|---|
| T-D-011 | Production smoke test (vuln-fix sonrası) | 2026-05-07 | Kullanıcı onayı ile pass. Sprint #2 closed. |
| **T-D-007** 🚨 | **Critical vuln fix** SemanticKernel 1.54 → 1.75 + transitive `Microsoft.Extensions.*` 9.0.5 → 10.0.7 | 2026-05-07 | Build ✅ (0 warn, 0 err) · `dotnet list package --vulnerable` ✅ temiz. Production smoke ✅. Commit `d3a1887`. |
| T-D-008 | `System.IdentityModel.Tokens.Jwt` 8.3.0 → 8.18.0 | 2026-05-07 | Auth regression — compile OK, JWT validation API'si değişmemiş. |
| T-D-001 | `PROJECT_CONTEXT.md` doldur | 2026-05-07 | Status: Production. Hedef kullanıcılar + KPI'lar + Out-of-scope yazıldı. |
| T-D-002 | `ARCHITECTURE.md` doldur | 2026-05-07 | Stack, components, integrations, kritik veri akışları, mimari kararlar, tech debt listesi. |
| T-D-003 | `TECH_RADAR.md` mevcut stack haritalandı | 2026-05-07 | 13 Adopt + 2 Trial (PGvector, Embedding API) + 4 Assess + 3 Hold. |
| T-D-004 | Dependency audit (`dotnet list package --vulnerable --outdated`) | 2026-05-07 | 🚨 1 Critical (SemanticKernel.Core) + ~15 minor outdated tespit edildi. T-D-007 ve T-D-008 görevleri açıldı. |
| T-D-005 | RAG migration brief | 2026-05-07 | `RESEARCH_BRIEFS/rag-migration.md` — 6 fazlı plan, 7-9 gün toplam, Sprint #4 kapsamı. |
| T-D-006 | Brief index'i güncelle | 2026-05-07 | `RESEARCH_BRIEFS/README.md` rag-migration.md'yi gösterir. |
| T-000 | Enterprise takım kurulumu | 2026-05-07 | `~/.claude_enterprise_team.md` + global CLAUDE.md + `.claudeteam/` |
| T-000b | AI Mühendisliği sub-team eklendi | 2026-05-07 | Agent / Context-Workflow / ML-RAG rolleri. ADR-002. |
| T-000c | Code Review + Cyber Security elevate edildi | 2026-05-07 | Tech Lead rolü + AppSec/SecOps/Red Team ayrımı + 5.6 (review workflow) + 5.7 (risk tiers). ADR-003. |
| T-000d | Clean Code disiplini eklendi | 2026-05-07 | Section 5.8 Manifesto. ADR-004. |
| T-000e | Araştırma & İnovasyon sub-team eklendi | 2026-05-07 | Tech Radar / Innovation Architect / Knowledge Curator + Section 5.9. ADR-005. |
| T-000f | `/enterpriseteam` slash command + global template | 2026-05-08 | `~/.claude/commands/enterpriseteam.md` + `~/.claude/team-template/` (7 dosya). Smart routing (init / status / sprint / decisions / tech). ADR-008. |
| T-000g | DevOps & SRE sub-team + RUNBOOKS mekanizması | 2026-05-08 | Dev #2 data-refocus, yeni 2-rollü DevOps team, Section 5.10 (Workflow + Emergency Recovery), 9 anti-pattern, 13 risk signal. `RUNBOOKS.md` template + AykutOnPC instance (`D:\AYKUTONPC-VPS-REHBERI.md` referans). ADR-009. |

---

## Sıradaki Sprint'ler (planlama)

- **Sprint #4 — RAG migration** (2-3 hafta)
  - `RESEARCH_BRIEFS/rag-migration.md` plan'ına göre
  - Faz 1-6 → PGvector + EmbeddingService + KB integration + chat swap + eval + adoption
  - Spike time-box: Faz 1-2 için 3 gün

- **Backlog (sırası belirsiz):**
  - Test projesi kurulumu (xUnit/NUnit, integration test, Testcontainers ile Postgres)
  - CSP header eklenmesi (`Program.cs` security middleware)
  - Structured logging (Serilog → file + Seq/Loki opsiyonel — Tech Radar Assess'te)
  - .NET 9 → .NET 10 LTS upgrade (Kasım 2026 sonrası — ayrı sprint)
  - Redis healthcheck'i ya kullan (distributed cache) ya kaldır (`ARCHITECTURE.md` tech debt)

---

## Bloklayıcılar

- Yok.
