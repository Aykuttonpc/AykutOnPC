# Brief: KB Stuffing → Gerçek RAG (PGvector + Embedding)

**Kategori önerisi:** Trial → Adopt (Sprint #4 implementasyonu sonrası)
**Tarih:** 2026-05-07
**Hazırlayan:** Knowledge Curator (AykutOnPC takımı)

---

## Ne?

Mevcut chat retrieval **"RAG değil, retrieval lite"**: `KnowledgeBaseService.GetCachedForChatAsync(50)` her sorguda son 50 KB entry'yi alıyor → tamamı tek system message'a yığılıyor → LLM'e gönderiliyor.

**Hedef:** Klasik RAG akışı:
1. User mesajı → embedding vektörü
2. PostgreSQL'de **PGvector extension** ile cosine similarity search → top-K (örn. 5) en alakalı entry
3. Sadece bu top-K entry system prompt'a girer
4. LLM call (mevcut Gemini 2.5 Flash, değişiklik yok)

## Neden ilgili?

Spesifik dert listesi:

| Şu anki problem | RAG sonrası |
|---|---|
| 50 entry × ~200 token = ~10K token her chat'te system prompt'a giriyor | Top-5 entry × ~200 token = ~1K token (~%90 azalma) |
| Alakasız KB entry de prompt'a giriyor → noise | Sadece soruya yakın entry'ler → relevance ↑ |
| KB büyüdükçe (>100 entry) ya truncate ya context limit problemi | KB scale eder (1000+ entry mümkün, retrieval hep top-K) |
| "Aykut'un React tecrübesi?" sorusunda Java entry de prompt'ta | Sadece JS/React/Frontend entry'leri retrieved |

## Olgunluk

**PGvector:**
- Production-ready, 2024'ten beri stable. Supabase, Neon, AWS RDS Postgres'te native destek.
- GitHub: 13K+ star, aktif maintainer, breaking change geçmişi yok
- Postgres 16 ile uyumlu — mevcut DB'ye `CREATE EXTENSION vector` ile eklenir
- Doküman kalitesi yüksek

**Embedding modelleri:**
- **Gemini text-embedding-004** — 768-dim, free tier var (request bazlı limit). Mevcut Gemini API key kullanılabilir.
- **OpenAI text-embedding-3-small** — 1536-dim, $0.02 / 1M token (çok ucuz)
- Her ikisi de OpenAI-compatible REST endpoint sunar → mevcut `OpenAIToTargetHandler` pattern'iyle uyumlu

## Trade-off'lar

**(+) Avantajlar:**
- Token maliyeti ~%90 azalır (chat başına ~9K token tasarruf)
- KB scale edebilir (sınır ortadan kalkar)
- Relevance artar — alakasız entry'ler kaybolur
- Multi-language soft support (embedding semantic — TR/EN karışık çalışır)
- Hybrid search'e (BM25 + vector) açık kapı bırakır

**(−) Dezavantajlar:**
- Her chat'te ek embedding call (latency: 50-200ms)
- Embedding model değişirse **re-index** gerekli (versioning lazım)
- Cold-start: embedding'i eksik entry'ler retrieval'a girmez (backfill script lazım)
- KB Create/Update'te embedding compute zorunlu → admin save işlemi yavaşlar (~200ms)

**Vendor lock-in:** Düşük. PGvector açık kaynak, embedding API'si provider-agnostic (Gemini/OpenAI/yerel sentence-transformers değiştirilebilir).

**Lisans:** PGvector PostgreSQL License (BSD-style), commercial-friendly.

## Güvenlik Geçmişi

- **PGvector:** Bilinen CVE yok. Postgres extension'ı, ana DB güvenliğine miras.
- **Embedding API'leri:** Provider'a bağlı (Gemini/OpenAI standart key auth).
- **Indirect prompt injection riski:** KB content admin-only yazılır → düşük risk. Ama yeni RAG akışında "retrieved content her zaman trust'lanır" assumption'ı devam eder. AppSec onayı: KB content'in HTML escape edildiğinden emin ol (zaten Razor output encoding ile koruma var).
- **Embedding leak:** Embedding vektörleri DB'de plaintext (encrypt'lenmiyor). Hassas bilgi varsa farkında ol — ama portfolyo KB'sinde sensitive data yok.
- **AppSec onayı:** ✅ (mevcut threat surface'i artırmıyor)

## Önerilen Aksiyon

**Trial pilot — Sprint #4 (2-3 hafta):**

### Adım adım plan

**Faz 1 — Altyapı (1-2 gün):**
1. Postgres 16 üzerine PGvector extension kur (`CREATE EXTENSION IF NOT EXISTS vector;`)
2. EF Migration: `KnowledgeEntry.Embedding` kolonu ekle (`vector(768)` Gemini için)
3. Index oluştur: `CREATE INDEX ... USING hnsw (embedding vector_cosine_ops)` (HNSW, IVFFlat'tan üstün)
4. NuGet: `Pgvector.EntityFrameworkCore` ekle (mevcut Npgsql ile uyumlu)

**Faz 2 — Embedding service (1 gün):**
5. `IEmbeddingService` interface (Core)
6. `GeminiEmbeddingService` impl (Infrastructure) — `text-embedding-004` endpoint
7. DI registration (`ServiceExtensions`)
8. Config: `AiSettings.EmbeddingModelId`, `EmbeddingDimensions`

**Faz 3 — KB integration (1 gün):**
9. `KnowledgeBaseService.CreateAsync` / `UpdateAsync` → embedding compute → save
10. `KnowledgeBaseService.GetRelevantForChatAsync(string query, int topK)` — vector similarity query (`ORDER BY embedding <=> @queryVec LIMIT @topK`)
11. **Backfill command** (CLI): `dotnet AykutOnPC.Web.dll --reindex-kb` — mevcut entry'lerin embedding'ini hesaplar

**Faz 4 — Chat flow swap (1 gün):**
12. `AiService.BuildChatHistoryAsync` → eski `GetCachedForChatAsync(50)` yerine `GetRelevantForChatAsync(userMessage, topK=5)`
13. Cache stratejisi: same-query LRU cache opsiyonel (chat'te tekrar düşük olduğundan zorunlu değil)
14. Feature flag: `AiSettings.UseRagRetrieval` — kapanırsa eski davranışa düşer (rollback için)

**Faz 5 — Eval + monitoring (2-3 gün):**
15. Eval set: 30-50 soru-beklenen-entry çifti (manuel hazırlanır, ChatLogs'tan ilham alınır)
16. LLM-as-judge regression: GPT-4o veya Gemini Pro eval'lerini bizim çıktıyla karşılaştırır
17. Token cost monitoring: ChatLogs'a `PromptTokens`, `CompletionTokens`, `EmbeddingLatencyMs` kolonları ekle
18. A/B test: `UseRagRetrieval` feature flag ile 1 hafta paralel ölç (eski vs yeni)

**Faz 6 — Adoption + cleanup (0.5 gün):**
19. A/B sonucu Adopt'a yeterse `UseRagRetrieval=true` permanent
20. Eski `GetCachedForChatAsync` deprecate (ama feature flag rollback için 1 sprint daha tut)
21. ADR yaz, TECH_RADAR'da PGvector + embedding Adopt'a taşı

### Time-box

**Toplam: 7-9 gün** (Sprint #4 kapsamına oturur — 2-3 hafta dilimi rahat)

Spike (Faz 1-2) **3 günü** geçerse → dur ve raporla. PGvector'un Hetzner Postgres container'ında problem yapması olası → fallback: Qdrant standalone container.

## Maliyet hesabı

- **Gemini text-embedding-004:** Free tier RPM/RPD limitleri var ama portfolyo trafiği düşük (10-50 chat/gün) → ücretsiz kalır
- **PGvector:** Mevcut Postgres üzerine extension, ek maliyet yok
- **Storage:** 768-dim float = 3KB / entry → 1000 entry = 3MB → ihmal edilebilir
- **Re-index maliyeti:** Tek seferlik ~50-100 embedding call (mevcut KB boyutu) → ücretsiz tier yeter

## Geri alma planı (rollback)

- Feature flag `UseRagRetrieval=false` → eski davranışa anında dön
- DB schema değişikliği geri alınmaz (vector kolonu kalır, kullanılmaz) — yeni sprint'te kaldırılırsa down migration yazılır
- Production'da issue çıkarsa: Hetzner'a SSH'la `.env.prod` flag çevir, container restart

## Referanslar

- PGvector: https://github.com/pgvector/pgvector
- Pgvector.EntityFrameworkCore: https://github.com/pgvector/pgvector-dotnet
- Gemini Embeddings: https://ai.google.dev/gemini-api/docs/embeddings
- OpenAI Embeddings: https://platform.openai.com/docs/guides/embeddings
- HNSW vs IVFFlat (PGvector): https://github.com/pgvector/pgvector#indexing
- SemanticKernel embedding desteği: https://learn.microsoft.com/en-us/semantic-kernel/concepts/text-search/

## Açık sorular (Sprint #4 başlamadan netleşmesi gereken)

1. Embedding provider: Gemini (mevcut key, free tier) **mi** OpenAI (~$0.02/1M token, daha olgun ekosistem) **mi**?
2. `topK` değeri: 3 / 5 / 10? Eval set ile A/B test edilmeli, başlangıç önerisi **5**.
3. Hybrid search (BM25 + vector) Sprint #4'e dahil mi yoksa Sprint #5'e mi? Önerim: pure-vector ile başla, ihtiyaç doğarsa sonra ekle.
4. Eval set'i kim hazırlayacak — manual mi ChatLogs'tan otomatik mi? Önerim: ChatLogs'tan top 30 sorudan başla + Aykut manuel 20 soru ekler.
