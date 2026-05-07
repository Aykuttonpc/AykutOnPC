# Sprint Board

> Aktif sprint. WIP'i bir kişide max 1-2 ile sınırla.
> Tarihler: ISO format (YYYY-MM-DD).

## Aktif Sprint

- **Sprint:** #2 — Bootstrap Tamamlama + RAG Brief
- **Başlangıç:** 2026-05-07
- **Hedef bitiş:** 2026-05-12 (3-5 gün)
- **Sprint hedefi:** Proje context'ini koddan derive edip yazılı hale getirmek (PROJECT_CONTEXT, ARCHITECTURE, TECH_RADAR), RAG migration için brief üretmek, dependency audit'inde tespit edilen Critical vuln'u kapatmak.

---

## 📋 Todo

| ID | Başlık | Sahip | Tahmin | Notlar |
|---|---|---|---|---|
| T-D-011 | **Production smoke test** — vuln-fix sonrası chat/auth runtime doğrulaması | Aykut (kullanıcı) | 5dk | `bash scripts/smoke-test.sh https://aykutonpc.com` + chat'e 1 mesaj. Dev'de chat fail eder (memory feedback) — sadece prod'da güvenli test edilir. |
| T-D-009 | `Pgvector.EntityFrameworkCore` package'ı POC için Trial — Sprint #4'e bırakıldı | Innovation Architect | — | Brief yazıldı, implementation Sprint #4 |

## 🚧 In Progress

| ID | Başlık | Sahip | Başlangıç | Notlar |
|---|---|---|---|---|

## 👀 Review

| ID | Başlık | Sahip | PR/Branch | Bekleyen |
|---|---|---|---|---|

## ✅ Done

| ID | Başlık | Tamamlanan | Notlar |
|---|---|---|---|
| **T-D-007** 🚨 | **Critical vuln fix** SemanticKernel 1.54 → 1.75 + transitive `Microsoft.Extensions.*` 9.0.5 → 10.0.7 | 2026-05-07 | Build ✅ (0 warn, 0 err) · `dotnet list package --vulnerable` ✅ temiz. Runtime test prod smoke'a kalır (T-D-011). |
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

---

## Sıradaki Sprint'ler (planlama)

- **Sprint #3 — Blog modülü** (1.5-2 hafta)
  - `BlogPost` entity (markdown content + slug + published_at + tags)
  - Admin CRUD (mevcut KnowledgeBase pattern'i)
  - Public `/blog` listing + `/blog/{slug}` detail (Razor View)
  - RSS feed (`/blog/feed.xml`)
  - SEO: `<meta>` tags, sitemap.xml entry
  - Test: yeni blog post submit + publish + render path

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
