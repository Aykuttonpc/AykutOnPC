# Architecture

> Sistemin teknik resmi. Yeni katılan dev bunu okuyup orient olabilmeli.
> Code'dan derive edilebilen şeyleri DUPLICATE etme — sadece **non-obvious** olanları yaz.

## Stack Özeti

- **Frontend:** ASP.NET Razor Views (server-rendered) + vanilla JS chat widget + Chart.js (admin dashboard). Cyberpunk-themed CSS.
- **Backend:** .NET 9 · ASP.NET Core MVC · `Microsoft.SemanticKernel` 1.54
- **Database:** PostgreSQL 16 · EF Core 9 (Npgsql provider). Repository pattern **yok** — `AppDbContext` doğrudan abstraction.
- **Auth:** JWT token + cookie hibrit. Token `AykutOnPC.AuthToken` cookie'sinde saklanır, `JwtBearerEvents.OnMessageReceived` cookie'den okur. BCrypt (configurable work factor) password hash.
- **AI/LLM:** Gemini 2.5 Flash (default) via OpenAI-compatible endpoint (`generativelanguage.googleapis.com/v1beta/openai`). Provider switch için `OpenAIToTargetHandler` (HTTP message handler) endpoint'i çevirir.
- **Cache:** In-process `IMemoryCache` (KB için 10dk TTL, GitHub repos için configurable).
- **Hosting / Deploy:** Hetzner CX22 (Falkenstein) · Ubuntu 24.04 · Docker Compose · Nginx reverse proxy + Let's Encrypt · GitHub Actions CI/CD `main` push'da auto-deploy.

## Major Components

```
AykutOnPC.Core
├── Entities          User, Profile, Experience, Education, Spec, Build, KnowledgeEntry, PageView, ChatLog
├── Configuration     JwtSettings, AiSettings (alias "GeminiSettings"), GitHubSettings, SecuritySettings, SeedDataSettings
├── DTOs              Profile/Auth/Chat/Entity/VisitorStats DTOs
└── Interfaces        I{Profile,Education,Experience,Spec,KnowledgeBase,Auth,AI,GitHub,VisitorAnalytics,ChatLog}Service

AykutOnPC.Infrastructure
├── Data              AppDbContext (UTC DateTime ValueConverter forced for all entities), AppDbContextFactory (design-time)
├── Migrations        InitialPostgres → AddVisitorIntelligence → AddChatLogs
├── Services          {Profile,Education,Experience,Spec,Auth,Ai,GitHub,VisitorAnalytics,ChatLog,KnowledgeBase}Service
└── HttpHandlers      OpenAIToTargetHandler — rewrites OpenAI-style requests to provider's actual endpoint

AykutOnPC.Web
├── Controllers       Home, Account, Admin, Profile, Education, Experience, Specs, KnowledgeBase, Chat, ChatLogs
├── Infrastructure    VisitorTrackingMiddleware, GlobalExceptionHandler, RedisHealthCheck
├── Commands          SeedAdminCommand (`--seed-admin` CLI flag, run-and-exit)
├── Extensions        ServiceExtensions (DI wiring, JWT, rate limit, CORS, health checks)
├── Models            ErrorViewModel, DashboardViewModel
└── Program.cs        Composition root + middleware pipeline + background DB migrate
```

## External Integrations

| Servis | Amaç | Kritiklik |
|---|---|---|
| **Gemini API** (OpenAI-compatible) | AI chat completion (streaming + non-stream) | **Yüksek** — chat'in tek bağımlılığı |
| **GitHub API** | "Latest Builds" feed (cached, configurable TTL) | Düşük — fail olunca skeleton kalır |
| **Hetzner Cloud** | VPS hosting | Yüksek — site'nin altyapısı |
| **Let's Encrypt** | TLS sertifikası (Certbot, auto-renew) | Yüksek — HTTPS şart |
| **Redis** (production only, optional) | Healthcheck'te referans var ama kullanım için DI yok — `RedisHealthCheck` kontrolü yapılıyor | Düşük (şu an pasif) |

## Veri Akışı (Kritik Path'ler)

### 1. Ziyaretçi → Sayfa görüntüleme
Tüm istekler `VisitorTrackingMiddleware`'den geçer → IP SHA-256 hash + günlük rotating salt → `PageView` insert. Admin path'leri, bot user-agent'ları ve authenticated user'lar **dışlanır** (commit `b99519c`).

### 2. Ziyaretçi → AI chat
`POST /api/chat/ask` (rate-limit: 10/min) → `ChatController` → `AiService.AnswerAsync` (veya streaming versiyonu) →
1. `KnowledgeBaseService.GetCachedForChatAsync(50)` → son 50 KB entry (10dk cache)
2. System prompt + tüm KB content **TEK system message** olarak birleştirilir → bu "RAG değil, retrieval lite" (Sprint #4'ün hedefi)
3. `ConversationMemoryTurns=6` → ChatLog'dan son 6 turn `ChatHistory`'ye replay (alternating user/assistant)
4. SemanticKernel `IChatCompletionService` → Gemini → token stream
5. Sonuç `ChatLogService.LogAsync` → DB'ye yazılır (telemetry + future memory)

### 3. Admin → İçerik yönetimi
`POST /Account/Login` (rate-limit: 5/min/IP) → BCrypt verify → JWT token üret → `AykutOnPC.AuthToken` cookie'ye yaz (HttpOnly+Secure). `[Authorize]` endpoint'lerde `OnMessageReceived` cookie'den okur.

### 4. Production deploy
`git push origin main` → GitHub Actions `ci.yml` → `dotnet test` + `docker build` → SSH'la Hetzner'a → `scripts/deploy.sh`:
1. Pre-deploy DB backup
2. `docker compose pull && build`
3. Rolling restart
4. `/health` poll (60s timeout)
5. **Fail → otomatik rollback** to previous image tag

## Önemli Mimari Kararlar (Kısa Liste)

> Detay için `DECISIONS.md`. Bu liste sadece **bu proje** için olanlar.

- **Repository pattern yok** — `AppDbContext` doğrudan service'lere inject. Trade-off: test zorluğu, ama kod sade.
- **`AiSettings.SectionName = "GeminiSettings"`** — section name backwards-compat için. ENV var: `GEMINI_API_KEY`.
- **HTTP message handler ile endpoint çevirimi** — `OpenAIToTargetHandler` provider switch'e izin verir (Groq→Gemini geçişi commit `c1acafd`).
- **UTC DateTime ValueConverter** — `AppDbContext.OnModelCreating` her DateTime'i UTC stamp'ler. Sebep: Npgsql 6+ `timestamp with time zone` kolonlarına `Kind=Unspecified` yazınca patlıyor; admin formlarındaki `<input type="date">` `Unspecified` üretir → tek noktadan düzelt.
- **Background DB migrate** — `Program.cs` `Task.Run` ile migrate eder. Sebep: Render/proxy port-scan timeout'a düşüyordu; healthcheck request'lerini blocklamasın diye fire-and-forget.
- **DataProtection persisted to filesystem** — Render/Docker'da ephemeral storage = login formları 500 atıyordu. `SecuritySettings.DataProtectionPath` fix.
- **Rate limit politikaları:** ChatApi 10/min, General 60/min, Login 5/min/IP.
- **Auth cookie name:** `AykutOnPC.AuthToken`.

## Bilinen Sınırlamalar / Tech Debt

| Item | Sebep | Sprint hedefi |
|---|---|---|
| **Test dosyası yok** (CI'da `dotnet test` çalışıyor ama proje yok) | İlk versiyonda hız öncelikti | Sprint #2 sonrası değerlendirilecek |
| **"RAG" değil, retrieval lite** — KB'nin tamamı (50 entry) her sorguda system prompt'a yığılıyor | İlk MVP'de yeter sayıldı, KB küçük | **Sprint #4 — RAG migration** (brief Sprint #2'de yazılacak) |
| **Memory cache in-process** — multi-node deploy'da inkonsistent olur | Single-instance Hetzner CX22 yeter | Multi-node ihtiyacı doğunca Redis swap |
| **Eval set yok** | LLM regression testi henüz kurulmadı | Sprint #4 RAG ile birlikte |
| **CSP header yok** | `Program.cs` security header'ları yazıyor ama `Content-Security-Policy` eksik | Sprint #2 / Tema A polish'e ekle |
| **`ChatLog.BotResponse` MaxLength 8000** | Çok uzun cevaplar truncate olur | Düşük öncelik, KB cevapları kısa |
| **Redis healthcheck var ama Redis kullanılmıyor** | Production'da Redis container kalktı, ama DI yok | Ya kaldır ya gerçek kullan (cache layer) |
| **Background migrate'in retry limit'i 5** | Yeterli ama log dışında alarm yok | Observability tema |
