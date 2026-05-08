# Sprint Board

> Aktif sprint. WIP'i bir kişide max 1-2 ile sınırla.
> Tarihler: ISO format (YYYY-MM-DD).

## Aktif Sprint

- **Sprint:** #5 — Henüz planlanmadı
- **Önceki kapanış:** Sprint #4 (RAG + Visitor ID + Inbox) — 2026-05-09
- **Sprint hedefi:** Belirlenecek (backlog'dan veya yeni hedef)

---

## 📋 Todo

_Boş — Sprint #5 planlanmadı, backlog'dan iş çekilecek._

## 🚧 In Progress

| ID | Başlık | Sahip | Başlangıç | Notlar |
|---|---|---|---|---|

## 👀 Review

| ID | Başlık | Sahip | PR/Branch | Bekleyen |
|---|---|---|---|---|

## ✅ Done

### Sprint #4 (kapatıldı 2026-05-09 — incident geçirdi, recovered, ADR-013)

| ID | Başlık | Tamamlanan | Notlar |
|---|---|---|---|
| T-#4-002 | LocalStorage anonim visitor ID | 2026-05-09 | `PageView.VisitorId Guid?` + EF migration `AddVisitorId` + `IX_PageViews_VisitorId`. `VisitorTrackingMiddleware` cookie+header okuma. `_Layout.cshtml` <head> inline UUID gen + cookie set. `VisitorAnalyticsService` unique sorgular `COALESCE(VisitorId::text, HashedIp)`. ADR-011. Build ✅ 0w/0e. Lokal davranış doğrulama prod'da yapılacak (dev chat fail expected). |
| T-#4-001 | RAG migration (Faz 1-6) | 2026-05-09 | **Faz 1:** PGvector image (dev=pg15, prod=pg16) + `Pgvector.EntityFrameworkCore` 0.3.0 + EF Core 9.0.4 bump. Migration `AddKnowledgeEmbedding` (CREATE EXTENSION + vector(768) + HNSW raw SQL). `KnowledgeEntry.Embedding Vector?`. **Faz 2:** `IEmbeddingService` + `GeminiEmbeddingService` (native :embedContent, exp backoff retry, transient classification). `EmbeddingSettings` config. **Faz 3:** KB Create/Update embed compute, `SearchSimilarAsync(query, topK)` (cosine via `<=>` HNSW), `BackfillMissingEmbeddingsAsync()`, `--backfill-embeddings` CLI. **Faz 4:** `AiService.BuildChatHistoryAsync` RAG path (UseRagRetrieval=true, RetrievalTopK=5) + fallback to KB-stuffing on retrieval miss. **Faz 5:** `.claudeteam/EVAL_SET/rag-eval.json` (15 Q&A) + `--eval-rag` CLI keyword-check. **Faz 6:** ADR-012. Build ✅ 0w/0e. |
| T-#4-003 | Visitor Question Inbox | 2026-05-09 | `ChatLog`'a `IsReviewed`, `AdminNote`, `LinkedKnowledgeEntryId` + EF migration `AddChatLogInboxFields` + `IX_ChatLogs_Reviewed_CreatedAt`. `IChatLogService` Inbox metodları (`GetInboxAsync`, `GetByIdAsync`, `MarkReviewedAsync`, `LinkToKnowledgeEntryAsync`, `CountUnreviewedAsync`). Yeni `InboxController` `/admin/inbox` (Index, Detail, ToggleReview, PromoteToKb). Razor view'lar. `_AdminLayout` `@inject` ile badge + Inbox link. **Closed feedback loop** — soru gelir, Aykut KB'ye eklerse `KnowledgeBaseService.CreateAsync` otomatik embed eder, bir sonraki RAG retrieval'da entry bulunur. Build ✅ 0w/0e. |

### Sprint #3 (kapatıldı 2026-05-08)

| ID | Başlık | Tamamlanan | Notlar |
|---|---|---|---|
| T-#3-011 | Bot regex sıkılaştırma + **prod smoke** | 2026-05-08 | `VisitorTrackingMiddleware.BotPattern`: 20+ scanner family eklendi (zgrab, Censys, LeakIX, Modat, VisionHeight, Nuclei, Nikto, WPScan, sqlmap, masscan, httpx, Netcraft, Qualys, Shodan, Expanse, FOFA, …). Commit `6808377`. **Prod doğrulama:** 6 bot UA curl → **0 kayıt** ✅, 1 real UA Chrome → 1 kayıt ✅. UA/IP breakdown analiz Sprint #4'e ertelendi (RAG ile çakışmasın). |
| T-#3-013 | VI veri retention policy + cron + **kontamine veri purge** | 2026-05-08 | `scripts/prune-pageviews.sh` (PV=30g, CL=90g, env var override, auto-VACUUM). `ARCHITECTURE.md` "Veri Retention Policy" bölümü. **Production:** kontamine 124 PageView DELETE'lendi (count 124→0), crontab `0 4 * * *` aktif, script `/opt/aykutonpc/scripts/prune-pageviews.sh` deploy edildi. |
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

- **Sprint #5** — henüz hedef belirlenmedi. Backlog'dan veya yeni iş.

- **Backlog (sırası belirsiz):**
  - **CSP header** eklenmesi (`Program.cs` security middleware) — AppSec eski isteği, basit
  - **Structured logging** (Serilog → file + Seq/Loki opsiyonel — Tech Radar Assess'te)
  - **Redis healthcheck'i** ya kullan (distributed cache) ya kaldır (`ARCHITECTURE.md` tech debt)
  - **.NET 9 → .NET 10 LTS upgrade** (Kasım 2026 sonrası — ayrı sprint)
  - **LLM-as-judge eval upgrade** — şu an `rag-eval.json` keyword check, ileride judge model ile semantic compare

---

## Bloklayıcılar

- Yok.
