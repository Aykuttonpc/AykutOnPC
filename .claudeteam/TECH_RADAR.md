# Tech Radar

> Canlı doküman. Tech Radar Engineer sahibi.
> Kategoriler: 🟢 Adopt | 🟡 Trial | 🔵 Assess | 🔴 Hold

---

## 🟢 Adopt (production'da güvenle kullanılır)

| Tech | Kategori | Eklendi | Not |
|---|---|---|---|
| .NET 9 | Runtime / Framework | 2026-05-07 | Mevcut runtime — LTS değil ama .NET 10 LTS Kasım 2026 (yükseltme planı sonra) |
| ASP.NET Core MVC | Web framework | 2026-05-07 | Server-rendered Razor — portfolyo için doğru pick (SPA overhead'i yok) |
| EF Core 9 + Npgsql | ORM + DB driver | 2026-05-07 | Migration disiplini var, retry-on-failure açık |
| PostgreSQL 16 | Database | 2026-05-07 | Single instance, daily backup + offsite rclone |
| Microsoft.SemanticKernel 1.54 | LLM orchestration | 2026-05-07 | OpenAI-compatible endpoint çevirimi ile provider-agnostic |
| Gemini 2.5 Flash | LLM model | 2026-05-07 | Default chat model. Free tier limit + maliyet uygun. Provider switch kolay (Groq→Gemini geçişi olmuş — `c1acafd`) |
| BCrypt.Net-Next | Password hashing | 2026-05-07 | Configurable work factor — iyi |
| JWT (cookie hibrit) | Auth | 2026-05-07 | Cookie içinde JWT — XSS'e karşı HttpOnly, CSRF için SameSite ayarı kontrolü gerekli |
| Docker Compose | Container orkestrasyon | 2026-05-07 | Single-host yeter, Kubernetes overkill |
| Hetzner Cloud CX22 | VPS | 2026-05-07 | €5/ay, 2vCPU+4GB — kişisel proje için doğru |
| Nginx + Let's Encrypt | Reverse proxy + TLS | 2026-05-07 | Auto-renew Certbot |
| GitHub Actions | CI/CD | 2026-05-07 | `main` push → auto-deploy, healthcheck rollback |
| Chart.js | Admin dashboard grafikleri | 2026-05-07 | Vanilla JS, küçük footprint |

## 🟡 Trial (düşük riskli pilot için hazır)

| Tech | Kategori | Eklendi | Brief | Pilot kapsamı |
|---|---|---|---|---|
| **PGvector** | Vector store (PG extension) | 2026-05-07 | [rag-migration.md](RESEARCH_BRIEFS/rag-migration.md) | Sprint #4 — KB → embedding tabanlı retrieval. Mevcut Postgres üzerine extension eklenir, ayrı vector DB gerekmez. |
| **Embedding API (Gemini text-embedding-004 / OpenAI text-embedding-3-small)** | Embedding model | 2026-05-07 | [rag-migration.md](RESEARCH_BRIEFS/rag-migration.md) | KnowledgeEntry'ler için 768-dim vector üretimi |

## 🔵 Assess (takip ediyoruz, brief var, henüz sıra değil)

| Tech | Kategori | Eklendi | Brief | Bekleme sebebi |
|---|---|---|---|---|
| **Aspire** | .NET orchestration / observability | 2026-05-07 | _yok henüz_ | Single-container deploy'da gereksiz; multi-service'e geçilirse değerlendirilir |
| **OpenTelemetry + Seq/Grafana Loki** | Structured logging + tracing | 2026-05-07 | _yok henüz_ | Polish/Observability sprint'inde brief yazılacak |
| **MinimalAPI / FastEndpoints** | Endpoint pattern | 2026-05-07 | _yok henüz_ | MVC zaten çalışıyor; migration zorunluluğu yok |
| **Bun / esbuild** | JS build tool | 2026-05-07 | _yok henüz_ | Vanilla JS yeterli, build pipeline yok şu an |

## 🔴 Hold (kullanma — eski/sorunlu/risk)

| Tech | Kategori | Eklendi | Sebep | Migration planı |
|---|---|---|---|---|
| **Groq Llama 3.3 70B** | LLM model | 2026-05-07 | Bedava tier ama latency + tutarlılık Gemini'den düşüktü | ✅ Migration tamamlandı (`c1acafd` — Groq → Gemini 2.5 Flash) |
| **Repository pattern** (.NET klasik) | Data access | 2026-05-07 | EF Core 9 zaten abstraction; ek katman = boilerplate | Eklenmeyecek (DECISIONS.md'de potansiyel ADR) |
| **Custom RAG infra (LangChain.NET vb.)** | LLM orchestration | 2026-05-07 | SemanticKernel 1.x stable; LangChain.NET olgun değil | SemanticKernel'da kal |

---

## Geçiş Tarihçesi

> Bir tech kategoriler arası taşındığında buraya not düşülür.

| Tarih | Tech | Eski → Yeni | Sebep | ADR |
|---|---|---|---|---|
| 2026-05-07 | Tech Radar boş → mevcut stack haritalandı | — | Sprint #2 Tema D — bootstrap | — |
| (önceki) | Groq Llama 3.3 70B | Adopt → Hold | Latency + safety filter aşırı agresif | commit `c1acafd` |
