# Decisions Log (ADR-Style)

> Her önemli teknik/ürün kararı buraya yazılır.
> Format: tarih ↑ olacak şekilde, en yeni en üstte.
> "Önemli" tanımı: 2 hafta sonra "neden böyle yaptık?" sorusunu doğurabilecek her şey.

---

## Şablon

```
### ADR-NNN — [Karar başlığı]

- **Tarih:** YYYY-MM-DD
- **Durum:** Önerildi / Kabul edildi / Geri çekildi / Yerine geçen ADR-XXX
- **Karar verenler:** [Roller / kişiler]

**Bağlam:**
[Neden karar gerekiyordu? Hangi problem?]

**Değerlendirilen Seçenekler:**
1. [Seçenek A] — [artısı/eksisi]
2. [Seçenek B] — [artısı/eksisi]

**Karar:**
[Hangisi seçildi]

**Rationale:**
[Neden bu seçildi]

**Sonuçlar / Trade-off'lar:**
[Bu kararın yan etkileri, kabul ettiğimiz dezavantajlar]
```

---

## ADR-010 — Visitor Intelligence Investigation + Production Lockout Incident

- **Tarih:** 2026-05-08
- **Durum:** Resolved
- **Karar verenler:** SRE, AppSec, Senior Dev #1, QA, Aykut

**Bağlam:**
Production'daki Visitor Intelligence dashboard verileri (33 visits today, 27 unique) Aykut'a gerçekçi gelmedi → DB raw sorgu için SSH gerekti. Bağlanma sırasında port 22 timeout, oysa site (80/443) sağlıklıydı. Hetzner Cloud Firewall ekli değildi → sorun **iç tarafta**: fail2ban'ın kalıcı ban DB'si (`/var/lib/fail2ban/fail2ban.sqlite3`) + nftables'ta IP-spesifik DROP kuralı bizim IP'lerimizi blokluyordu. Brute force IP'leri (213.177.179.80, 94.156.152.18, vb.) auth aşamasına geliyor, bizimkiler TCP-level düşüyordu.

**Recovery akışı:**
1. Hetzner Web Console'a giriş → Caps Lock + paste sorunu (yeni Console UI'da clipboard ikonu yok, password yanlış)
2. Rescue mode + SSH key auth → orijinal disk mount + chroot ile UFW file-level disable (`ENABLED=no`)
3. Reboot normal → port 22 hâlâ timeout (fail2ban DB persisted)
4. Web Console'dan: `fail2ban-client unban --all` → `systemctl stop fail2ban` → `rm fail2ban.sqlite3` → `nft flush ruleset` → `systemctl restart docker` → `systemctl restart ssh` → port 22 açıldı
5. SSH bağlantı geri → UFW + fail2ban geri etkinleştirildi → 8-sorgu VI tanı koşturuldu

**VI Bulgular:**
- **DB sadece 2 günlük veri (124 kayıt, 07-08 May)** — pruning policy veya volume reset belirsiz
- **Bot regex 5 scanner kaçırıyor:** `zgrab`, `Censys`, `LeakIX`, `Modat`, `VisionHeight` (kontaminasyon ~15 visit)
- **Aynı UA'dan 42 visit** (eski iPhone iOS 13.2.3 — 2019 release, bot ihtimali yüksek)
- **Admin path leak: 0 row** ✅ — authenticated bypass çalışıyor
- **Feed/sitemap traffic: 0 row** — yeni eklendi, henüz keşfedilmedi (normal)
- "Kimler girmiş?" cevabı: **bilemeyiz** (IP'ler SHA-256 + daily salt, by design GDPR)

**Aksiyonlar (Sprint #3 todo):**
- **T-#3-011:** Bot regex sıkılaştırma + UA/IP breakdown analizi
- **T-#3-012:** `D:\AYKUTONPC-VPS-REHBERI.md` runbook'a "IP ban recovery" akışı (bu ADR ile başladı)
- **T-#3-013:** VI veri retention policy (30 gün cron pruning + ARCHITECTURE doc)

**Sonuçlar:**
- (+) IP ban recovery pattern'i runbook'a kaydedildi (Section 5.10 madde "ASLA YAPMA pattern'i takım hafızası")
- (+) Bot regex hardening adayları somut listede — Sprint #3'te tek-PR'la fix edilebilir
- (+) Aykut'un endişesi tetikledi → veri kalitesi audit yapılmış oldu
- (+) Web Console'un Caps Lock + paste sorunu Rescue mode + key auth ile bypass edilebileceği doğrulandı
- (−) ~2 saat operasyonel kayıp (kullanıcı etkilenmedi, sadece SRE side)
- (−) Recovery sırasında **3 root password chat'te leak oldu** → eğitsel: Section 5.10 risk listesine "Şifreyi chat'e yapıştırma" eklendi (kullanıcının kuralı), runbook'ta zaten mevcut

**Etkilenen artefaktlar:**
- `D:\AYKUTONPC-VPS-REHBERI.md` (T-#3-012 ile güncellenecek)
- `.claudeteam/SPRINT_BOARD.md` (INC-001 + T-#3-011/012/013 eklendi)
- `.claudeteam/DECISIONS.md` (bu ADR)

---

## ADR-009 — DevOps & SRE Sub-Team + RUNBOOKS Mekanizması

- **Tarih:** 2026-05-08
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), PO, Tech Lead, SecOps, Senior Dev #2

**Bağlam:**
Kullanıcı `D:\AYKUTONPC-VPS-REHBERI.md` adlı kapsamlı bir VPS runbook'u olduğunu paylaştı: SSH/UFW recovery, container yönetimi, deploy, ASLA YAPMA listesi, multi-tenant strategy. Bu pattern ekibe resmi olarak entegre edilmek istendi: "bunu da DevOps engineer'lar için ekleyelim".

Mevcut yapıda **Senior Dev #2 — Platform / Veri** zaten Linux/Docker/CI/CD/observability sahibi idi. Ama:
- Production deploy disiplini, runbook authorship, incident response gibi DevOps'a özel disiplinler yazılı değildi
- Emergency recovery (out-of-band fallback) pattern'i yok
- DevOps anti-pattern'leri (UFW lockout, `down -v`, latest tag, vs.) explicit yasak değildi
- Runbook'ların discoverability'si yoktu (D:\ root'unda dağılmış)

**Değerlendirilen Seçenekler:**
1. Sadece Senior Dev #2'nin scope'una "DevOps disiplini" ibaresi ekle — eksisi: tek role 4 disiplin (data + linux + docker + ci/cd + observability + runbook), sığlık riski
2. Tek "DevOps Engineer" rolü ekle, SRE bits Dev #2'de kalsın — eksisi: SRE/incident response disiplini sahipsiz
3. **2-rollü DevOps & SRE sub-team** + Dev #2'yi data-focus'a kaydır + RUNBOOKS.md mekanizması — net ayrım, sahiplenme, runbook discovery

**Karar:**
Seçenek 3.

**Yapılan Değişiklikler:**

**Section 1:**
- Senior Dev #2 light refocus: "Backend Data / Platform" — PostgreSQL/EF Core/queries/migrations odaklı. Linux+dev Docker'ı korur, prod deployment + observability DevOps'a devredildi.
- **🚀 DevOps & SRE sub-team** — 2 rol:
  - **DevOps Engineer** — CI/CD, container orchestration, IaC, deploy/rollback, secrets at infra
  - **SRE / Platform Engineer** — uptime, observability, alerting, incident response, **runbook authorship**, emergency recovery

**Section 2 (table):** 6 yeni satır — production deploy, infrastructure değişikliği, CI/CD, container/compose, runbook authorship, prod DB migration

**Section 3 (hierarchy):** 6 yeni satır — deploy timing, CI/CD design, prod infra, incident response, runbook authorship, observability stack

**Section 5.10 [YENİ]** — DevOps & SRE Sorumluluk Alanı + Workflow:
- Üretilen artefaktlar tablosu (Dockerfile, .github/workflows, scripts/deploy.sh, infrastructure/, monitoring/, RUNBOOKS.md)
- Deploy disiplini (pre-deploy checklist + deploy + post-deploy + fail-rollback)
- **Emergency Recovery Pattern'i** — her runbook'ta out-of-band fallback (Web Console / IPMI / KVM)
- Standart Runbook bölümleri (7 zorunlu başlık)
- ASLA YAPMA pattern'i — runbook hijyeni
- Incident Response akışı (P0-P3 triage + blameless postmortem)
- RUNBOOKS.md format örneği

**Section 6:** 9 yeni anti-pattern — deploy-and-pray, ssh-and-fix, latest tag, secret in compose, UFW lockout, `down -v` panic, logs-to-stdout-only, sessiz monitoring, postmortem ihmali

**Section 7 (risk signals):** Yeni "DevOps / Infrastructure Riskleri" kategorisi — 13 madde (UFW lockout, volume-silen komut, latest tag, hardcoded secret, healthcheck yok, rollback yok, backup yok, SPOF, mem_limit yok, 0.0.0.0 bind, manuel SSH müdahale, log centralization yok, out-of-band recovery yok)

**Yeni Artefaktlar:**
- `~/.claude/team-template/RUNBOOKS.md` — operational runbook index template
- `D:\Projeler\Voxi\AykutOnPC\.claudeteam\RUNBOOKS.md` — AykutOnPC instance, `D:\AYKUTONPC-VPS-REHBERI.md`'yi referans alıyor

**Slash Command Güncellemesi:**
- `/enterpriseteam runbooks` argümanı eklendi (RUNBOOKS özet)
- `/enterpriseteam` status raporu RUNBOOKS.md'yi de okuyor
- `/enterpriseteam init` template kopyalama listesine `RUNBOOKS.md` eklendi

**Sonuçlar:**
- (+) DevOps disiplinleri yazılı ve ölçülebilir — sözel "infra'ya dikkat" yerine 13 risk sinyali, 9 anti-pattern, 7 zorunlu runbook bölümü
- (+) Emergency recovery norm'laştı — her runbook'ta out-of-band fallback zorunlu
- (+) Runbook'lar discoverable — `.claudeteam/RUNBOOKS.md` index'i her oturumda okunuyor
- (+) `D:\AYKUTONPC-VPS-REHBERI.md` artık takıma "görünmez" değil; SRE bunu DevOps işi geldiğinde okur
- (+) Senior Dev #2 ve DevOps team arasında net çizgi (data layer vs. infra layer)
- (−) Takım büyüdü (19 rol, 8 disiplin) — trivial-skip kuralı **çok sıkı** uygulanmalı
- (−) Senior Dev #2 description değişti — eski referanslar Section 2/3'te de hızlıca taranıp güncellenmeli (yapıldı)

**Etkilenen dosyalar:**
- `~/.claude_enterprise_team.md` — Section 1 (Dev #2 + DevOps team), Section 2/3 (yeni satırlar), Section 5.10 [yeni], Section 6 (9 anti-pattern), Section 7 (DevOps risk kategorisi)
- `~/.claude/team-template/RUNBOOKS.md` — yeni
- `~/.claude/commands/enterpriseteam.md` — RUNBOOKS desteği
- `D:\Projeler\Voxi\AykutOnPC\.claudeteam\RUNBOOKS.md` — yeni (AykutOnPC instance)

---

## ADR-008 — Takım Bootstrap için `/enterpriseteam` Slash Command

- **Tarih:** 2026-05-08
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), Context & Workflow Engineer, PO, Innovation Architect

**Bağlam:**
Takım promptu zaten global yüklenir (`~/.claude/CLAUDE.md` → `@~/.claude_enterprise_team.md`), ama yeni projeye girildiğinde:
- `.claudeteam/` template'lerini manuel kurmak friction
- Mevcut `.claudeteam/` varsa "nerede kaldık" raporunu manuel istemek tekrarlayan iş
- Kullanıcı: "mesela /enterpriseteam yazıp çağırabilsem takımı"

Bu, takım promptunun yokluğu değil — proje **context layer**'ının ayağa kaldırılması sorunu.

**Değerlendirilen Seçenekler:**
1. Shell function (orijinal `claude-team` öneri) — eksisi: PowerShell vs Bash ayrımı, terminal'e bağımlı, oturum içinden çağrılamaz
2. Tek "init script" + Claude'a manuel "status ver" demek — eksisi: iki ayrı işlem, hatırlanması lazım
3. **Native Claude Code slash command** — global `~/.claude/commands/enterpriseteam.md` — terminal-bağımsız, oturum içinden, smart routing (init vs status), argüman desteği

**Karar:**
Seçenek 3.

**Yapılan Değişiklikler:**
- **`~/.claude/team-template/`** (7 dosya) — single source of truth template'lar:
  - `PROJECT_CONTEXT.md`, `ARCHITECTURE.md`, `SPRINT_BOARD.md`, `DECISIONS.md`, `TEAM_NOTES.md`, `TECH_RADAR.md`, `RESEARCH_BRIEFS/README.md`
- **`~/.claude/commands/enterpriseteam.md`** — slash command:
  - Argüman desteği: `init` / `status` / `sprint` / `decisions` / `tech` / (boş)
  - Smart routing: `.claudeteam/` yoksa init önerisi, varsa status raporu
  - Standart status raporu formatı (Proje + Sprint + WIP + Son Done + ADR + Sıradaki)
  - Init akışında bugünün ISO tarihi otomatik doldurulur, ilk takım toplantısı (3 soru)
- **Prompt Section 10.5** — komut kullanım dokümantasyonu

**Sonuçlar:**
- (+) Yeni projede 30 sn'de bootstrap (`/enterpriseteam init` → onay → 7 dosya kopya → 3 soru → çalışmaya hazır)
- (+) Mevcut projede tek komutta orient olma (`/enterpriseteam` → status raporu)
- (+) Cross-platform (PowerShell/Bash ayrımı yok — Claude Code native)
- (+) Single source of truth: template güncellenirse `~/.claude/team-template/` tek yerde değişir
- (−) Template dosyaları zamanla update edilmeli (örn. yeni `.claudeteam/` standart dosyası eklenirse hem template hem mevcut projeler güncellenir)

**Etkilenen dosyalar:**
- `~/.claude/team-template/*.md` (7 yeni dosya)
- `~/.claude/commands/enterpriseteam.md` (yeni)
- `~/.claude_enterprise_team.md` (Section 10.5 eklendi)

---

## ADR-007 — SemanticKernel Critical Vuln Upgrade (1.54 → 1.75)

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi · Uygulandı (build ✅ · vuln re-check ✅)
- **Karar verenler:** AppSec, Senior Dev #1 (.NET), Tech Lead, PO

**Bağlam:**
Sprint #2 dependency audit'inde `Microsoft.SemanticKernel.Core 1.54.0` **Critical** seviyesinde tespit edildi ([GHSA-2ww3-72rp-wpp4](https://github.com/advisories/GHSA-2ww3-72rp-wpp4)). Transitif olarak hem `AykutOnPC.Infrastructure` hem `AykutOnPC.Web` etkileniyordu. Section 5.7 **Yüksek Risk** seviyesi (AI surface'i etkileyen kütüphane).

**Değerlendirilen Seçenekler:**
1. **1.54.x içindeki patch** — yok, advisory tüm 1.54.x'i etkiliyor
2. **1.55-1.74 ara version** — gereksiz, en güncel stable 1.75
3. **1.75'e atla** — 21 minor ama post-GA stabil API → seçildi
4. **Provider'ı SemanticKernel'dan değiştir** (örn. doğrudan Anthropic SDK / OpenAI SDK) — kapsam dışı, scope creep

**Karar:** Seçenek 3.

**Yapılan Değişiklikler:**
- `AykutOnPC.Infrastructure.csproj`:
  - `Microsoft.SemanticKernel` 1.54.0 → **1.75.0**
  - `Microsoft.SemanticKernel.Connectors.OpenAI` 1.54.0 → **1.75.0**
  - `System.IdentityModel.Tokens.Jwt` 8.3.0 → **8.18.0**
  - `Microsoft.Extensions.Configuration.Abstractions` 9.0.5 → **10.0.7** (transitive constraint — SemanticKernel 1.75 zorunlu kıldı)
  - `Microsoft.Extensions.Configuration.Binder` 9.0.5 → **10.0.7** (aynı sebep)
  - `Microsoft.Extensions.Caching.Abstractions` 9.0.5 → **10.0.7** (aynı sebep)
  - `Microsoft.Extensions.Logging.Abstractions` 9.0.5 → **10.0.7** (aynı sebep)

**Doğrulama:**
- `dotnet build` ✅ 0 warning, 0 error (12s)
- `dotnet list package --vulnerable --include-transitive` ✅ 3 projede de "no vulnerable packages"
- API uyumluluk ✅ — kullandığımız `Kernel`, `IChatCompletionService`, `AddOpenAIChatCompletion`, `ChatHistory`, streaming API'leri post-GA stabil; `OpenAIToTargetHandler` regression yok (HTTP rewrite layer SemanticKernel internal'ından bağımsız)

**Sonuçlar:**
- (+) Critical advisory kapandı — 3 proje temiz
- (+) `Microsoft.Extensions.*` 10.x .NET 9 runtime'ında çalışır (runtime-independent paketler)
- (−) **Runtime testi yapılmadı** — dev'de chat fail eder (memory feedback), production smoke test'i kullanıcıya bırakıldı (T-D-011)
- (−) `Microsoft.Extensions.*` 10.x → ileride .NET 10 LTS upgrade'inde diğer paketlerle senkronizasyon gerekir; not düşüldü

**Etkilenen dosyalar:**
- `AykutOnPC.Infrastructure/AykutOnPC.Infrastructure.csproj`

**Sonraki adım:** Aykut prod smoke test'i koşar, fail olursa rollback (`git revert`); pass ise sprint #2 kapanır.

---

## ADR-006 — Sprint #2 Bootstrap Tamamlama: Mevcut Stack Yazılı Hale Getirildi

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** PO, Tech Lead, Context & Workflow Engineer, AppSec, Knowledge Curator

**Bağlam:**
Sprint #1 sadece enterprise takım altyapısını kurmuştu (`.claudeteam/` şablonları). `PROJECT_CONTEXT.md`, `ARCHITECTURE.md`, `TECH_RADAR.md` boş template halindeydi → herhangi bir feature sprint'ini sağlıklı planlamak için "ne var, ne yok, ne risk taşıyor" sorularının yazılı cevabı yoktu. Kullanıcı 4 tema arasından (Polish, AI Maturity, Yeni Feature, Tech Debt) **Tema D + RAG brief** kombinasyonunu seçti.

**Karar:**
Sprint #2 = mevcut kodu mimariye dök + RAG migration için Section 5.9 standart brief'i yaz + Sprint #3 (Blog) ile #4 (RAG impl) sıraya koy.

**Yapılan iş:**
- `PROJECT_CONTEXT.md` — Production durumu, KPI'lar, kısıtlar, out-of-scope
- `ARCHITECTURE.md` — Stack, components, kritik path'ler, 8 mimari karar (UTC ValueConverter, background migrate, OpenAIToTargetHandler vb.), tech debt listesi
- `TECH_RADAR.md` — 13 Adopt + 2 Trial (PGvector, Embedding API) + 4 Assess + 3 Hold
- `RESEARCH_BRIEFS/rag-migration.md` — 6 fazlı plan (PGvector + EmbeddingService + KB swap + eval + adoption), 7-9 gün toplam
- Dependency audit → 1 **Critical** (`Microsoft.SemanticKernel.Core 1.54.0` — GHSA-2ww3-72rp-wpp4) + ~15 minor outdated tespit edildi

**Sonuçlar:**
- (+) Bootstrap'in eksik kalan içerik kısmı tamamlandı — sonraki sprint planlamaları artık yazılı bağlama oturur
- (+) Critical vuln tespit edildi → T-D-007 olarak Sprint #2'nin kalan kısmında acil görev
- (+) RAG roadmap netleşti — Sprint #4 kapsamı somut, time-box'lı
- (−) Sprint #2 başlangıçta 3-5 gün tahmin edildi, vuln fix'i sürdü → 5-7 gün olabilir

**Etkilenen dosyalar:**
- `.claudeteam/PROJECT_CONTEXT.md`, `ARCHITECTURE.md`, `TECH_RADAR.md`, `SPRINT_BOARD.md`, `DECISIONS.md`
- `.claudeteam/RESEARCH_BRIEFS/rag-migration.md` (yeni), `RESEARCH_BRIEFS/README.md` (index)

**Sonraki ADR'lar:**
- ADR-007 — SemanticKernel vuln upgrade sonrası (T-D-007 done olunca)
- ADR-008 — Sprint #4 RAG implementation kapanışı (Sprint #4 sonrası)

---

## ADR-005 — Araştırma & İnovasyon Sub-Team Eklendi

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), PO, Tech Lead, Tüm takım

**Bağlam:**
Üç birinci sınıf disiplin (Code Review, Cyber Security, Clean Code) takımı **doğru yapan** disiplinler — ama takımı **ileri taşıyan** bir motor eksikti. Kullanıcı şu eksikleri belirtti:
- Yeni teknolojileri sürekli takip eden bir mekanizma yok
- Mevcut probleme yaratıcı, farklı yollarla bakacak bir perspektif yok
- Yeni teknik kazanımları takıma öğretecek bir rol yok
- "İnternet'i sürekli araştırıp en üst düzey teknolojilere yönlendirecek" bir sub-team gerekli

Aynı zamanda risk: kontrolsüz yenilik = "shiny new toy" tuzağı. Hype-driven adoption, resume-driven development, premature adoption gibi tuzaklara düşmemek için **disiplinli bir keşif** lazım.

**Değerlendirilen Seçenekler:**
1. Mevcut Senior Dev'lere "ek olarak trend takip et" demek — eksisi: sahiplenme yok, hiçbiri yapmaz
2. Tek "Tech Radar Engineer" rolü ekle — eksisi: keşif + yaratıcı uygulama + öğretim üç farklı disiplin, tek role sığmaz
3. **3-rollü Araştırma & İnovasyon sub-team + disiplinli workflow** — sahiplenme net, hype filtresi var, adoption gates Tech Lead/PO/AppSec'te

**Karar:**
Seçenek 3.

**Yapılan Değişiklikler:**
- Section 1: Yeni **🔬 Araştırma & İnovasyon** sub-team — 3 rol:
  - **Tech Radar Engineer** — sürekli keşif, sinyal/gürültü ayrımı, TECH_RADAR.md sahibi
  - **Innovation Architect** — yaratıcı uygulama, cross-domain transfer, spike sahibi
  - **Knowledge Curator / Tech Educator** — brief yazma, takıma öğretim, RESEARCH_BRIEFS/ sahibi
- Section 2: Tartışma tablosuna 4 yeni satır (yeni tech önerisi, adoption, spike, migration)
- Section 3: Karar hiyerarşisine 5 yeni satır + "Innovation onay zinciri" çelişki kuralı
- **Section 5.9 [YENİ]** — Research & Innovation Workflow:
  - Tech Radar mantalitesi (Adopt/Trial/Assess/Hold — ThoughtWorks-style)
  - Standart Brief Formatı (.claudeteam/RESEARCH_BRIEFS/<slug>.md)
  - 7-soruluk Hype vs. Gerçek Değer Filtresi
  - Spike Workflow (zaman-kutulu, time-box aşımı = stop)
  - Adoption Disiplini checklist'i
  - Sürekli Tarama Rutinleri (HN, arxiv, Anthropic blog, vs.)
  - Davranış kuralları (sprint başına max 1-2 trial, %15-20 innovation budget)
- Section 6: 6 yeni anti-pattern — hype-driven adoption, NIH syndrome, resume-driven dev, premature adoption, innovation overload, sessiz tech debt

**Yeni Artefaktlar:**
- `.claudeteam/TECH_RADAR.md` (canlı doküman, kategorili tech listesi)
- `.claudeteam/RESEARCH_BRIEFS/<slug>.md` (her brief ayrı dosya)

**Sonuçlar:**
- (+) Yenilik takıma kontrollü şekilde girer; sahiplenme net
- (+) Hype filtresi sayesinde "Twitter'da gördüm, prod'a koyalım" tehlikesi engellenir
- (+) Brief disiplini sözel öneriyi yazılı analize dönüştürür
- (+) Adoption-zinciri (Tech Lead + PO + AppSec) yeni saldırı yüzeylerini engeller
- (−) Takım büyüdü (17 rol, 6 disiplin) — trivial-skip kuralı **çok sıkı** uygulanmalı
- (−) Brief yazma overhead'i var; minor öneriler için kısa-form brief'e izin verilebilir

**Etkilenen dosyalar:**
- `~/.claude_enterprise_team.md` — Section 1, 2, 3, 5.9 [yeni], 6

---

## ADR-004 — Clean Code Birinci Sınıf Disiplin Yapıldı

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), Tech Lead, Tüm takım

**Bağlam:**
Code Review (ADR-003) ve Cyber Security elevate edildikten sonra üçüncü disiplin olarak Clean Code eksik kalıyordu. Kullanıcı somut iki problem belirtti:
1. **Gereksiz dokümantasyon** — README şişmesi, function signature tekrarı, NE'yi anlatan yorumlar, stale yorum, tarihçe yorumu
2. **Gereksiz kontroller** — defansif validation fazlası, double-check, "log et ve devam et" try/catch'ler, paranoid null check zincirleri

Mevcut anti-pattern listesi (Section 6) bunlara dokunuyordu ama dağınıktı. Tech Lead review'da "bu kötü" dediğinde **referans göstereceği yazılı bir manifesto yoktu**.

**Değerlendirilen Seçenekler:**
1. Mevcut anti-pattern listesine madde eklemekle yetin — eksisi: dağınık, sistematik değil
2. Yeni rol "Clean Code Engineer" aç — eksisi: Tech Lead'le overlap, gereksiz role-fragmentation
3. **Yeni Section 5.8 — Clean Code Manifesto** + Tech Lead'i savunucu yap + DoD imza listesine ekle — net disiplin, role overhead'i yok

**Karar:**
Seçenek 3.

**Yapılan Değişiklikler:**
- Section 1: **Tech Lead** rolü genişletildi → "Clean Code Manifesto baş savunucusu"
- Section 4 (DoD): Tech Lead imzasına "Clean Code Manifesto madde madde geçti" ibaresi eklendi
- Section 5.6 (Code Review Workflow): Tech Lead step'i 5.8 referansıyla güçlendirildi (+5 yeni checklist item)
- **Section 5.8 [YENİ] — Clean Code Manifesto:**
  - 5 Temel Prensip (YAGNI, KISS, DRY-non-dogmatic, Cleverness<Clarity, Boy Scout Rule)
  - Dokümantasyon Disiplini (yazılmaz/yazılır listeleri + README disiplini)
  - Kontrol/Validation Disiplini (gereksiz defansif kod = false security)
  - Function/Class Boyut Kuralları (sayısal sinyaller, ihlal seviyeleri)
  - İsimlendirme Disiplini
  - Refactor Disiplini + karar ağacı
  - Test Hijyeni
  - Tech Lead Otomatik Bayrak Listesi (13 madde)
- Section 6 (Anti-Patterns): 9 yeni madde — README spam, stale yorum, generic naming, god function, magic number, bool flag, premature opt, refactor scope creep, defansif kod fazlası

**Sonuçlar:**
- (+) Tech Lead'in elinde **somut referans** — review'da "Section 5.8 madde X" diyebilir
- (+) Clean code artık subjektif değil; sayısal sinyaller (function 30+ satır, complexity 10+) var
- (+) Gereksiz dokümantasyon ve gereksiz validation **explicit yasak**
- (+) Refactor scope creep'e net sınır — bug fix'le refactor karışmıyor
- (−) Manifesto uzun (Section 5.8 ~150 satır) — ama lookup için yeterince yapılandırılmış
- (−) Pragmatik denge gerek: "her PR Manifesto'nun her maddesini" değil, **risk-tiered yaklaşım** Section 5.7'den miras alınır

**Etkilenen dosyalar:**
- `~/.claude_enterprise_team.md` — Section 1, 4, 5.6, 5.8 [yeni], 6

---

## ADR-003 — Code Review ve Cyber Security Disiplinleri Birinci Sınıf Yapıldı

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), Tüm takım

**Bağlam:**
Mevcut yapıda code review implicit'ti (Senior Dev'ler birbirini review eder varsayımı), güvenlik tek SecOps rolüne sıkıştırılmıştı. Kullanıcı bu ikisinin "en iyi hale" gelmesini istedi.

Tek SecOps şu yüklere bakıyordu:
- Kod-level OWASP risk taraması
- Operasyonel güvenlik (secrets, IAM, deploy)
- Adversarial threat modeling
- KVKK/GDPR compliance
- AI-spesifik güvenlik (prompt injection vs.)

Bu beş alan farklı disiplin — biri code reading, diğeri ops/network, üçüncüsü pen-test mindset. Tek role yığmak hepsinde sığ kalmaya yol açıyordu.

Code review tarafında ise yazarın kendi kodunu approve etmesi engelsiz idi → bias riski.

**Değerlendirilen Seçenekler:**
1. Mevcut yapıda kal, SecOps'a daha çok madde ekle — eksisi: aynı sığlık, role-overload
2. Kod review için yeni rol + güvenliği 2'ye böl (AppSec + SecOps) — eksisi: adversarial bakış hala SecOps'ta sığ
3. **Kod review için Tech Lead + güvenliği 3'e böl (AppSec + SecOps + Red Team)** + risk-tiered review derinliği — büyüme var ama disiplinler net

**Karar:**
Seçenek 3.

**Yapılan Değişiklikler:**
- Yeni rol: **Tech Lead / Senior Reviewer** — bias'sız peer review sahibi, self-approve yasak
- Eski SecOps → **3 role bölündü**: AppSec (kod-level), SecOps (ops), Red Team (adversarial)
- **Section 5.6** eklendi: 5-aşamalı Code Review Workflow (self → peer → security → adversarial → test)
- **Section 5.7** eklendi: 4-tier risk-bazlı Güvenlik Review Derinliği
- **Section 4 (DoD)** sıkılaştırıldı: explicit "✅ approved" imzaları, sessiz geçiş yasak
- **Section 6** anti-pattern'lerine 7 yeni madde: self-approve, security theater, patch-and-pray, dependency YOLO, sonra düzeltirim, sessiz security regression, logging hassas data
- **Section 3** çelişki kuralı: AppSec/SecOps her zaman ürün kararlarının üzerinde

**Sonuçlar:**
- (+) Code review birinci sınıf disiplin — hiçbir kod self-approve ile geçemez
- (+) Güvenlik 3 farklı zihniyetle kapsanıyor (kod / ops / adversarial)
- (+) Risk-tiered yaklaşım: trivial işlere overhead yok, kritik işlere derin inceleme
- (+) Explicit imza zorunluluğu → "geçti" demek somut, sözde değil
- (−) Trivial-skip kuralı sıkı uygulanmazsa overhead patlar — bu disiplin bizde
- (−) Yüksek risk işlerde `<team_discussion>` blokları daha uzun — kullanıcı bunu kabul etti

**Etkilenen dosyalar:**
- `~/.claude_enterprise_team.md` — Section 1, 2, 3, 4, 5.6 [yeni], 5.7 [yeni], 6

---

## ADR-002 — AI Mühendisliği Sub-Team Eklendi

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), PO, Mevcut takım

**Bağlam:**
Mevcut takımda AI işleri "Senior Dev #3 — AI & Modern Frontend" rolünde toplanmıştı. Tek bir kişide LLM entegrasyonu + agent geliştirme + frontend birleşince:
- Ne agent disiplini gerçek anlamda işliyordu (eval, boundary tasarımı yoktu)
- Ne de proje altyapısı (CLAUDE.md, skills, MCP) sahiplenilebiliyordu
- Frontend ve AI iki ayrı disiplin — birinin diğerine kanibalize olması istendi

Ayrıca kullanıcı, takıma katılan AI üyelerinin **proje içine somut artefakt üretmesini** istedi: `CLAUDE.md`, workflow MD'leri, custom agent'lar, MCP konfigürasyonları.

**Değerlendirilen Seçenekler:**
1. Mevcut Dev #3'ü genişlet — eksisi: tek kişiye 3 disiplin
2. AI'ı sadece Dev #3'te bırak, custom agent'ı SecOps'a yıkı — eksisi: rolün dışında, kalitesiz çıktı
3. **Ayrı 3-kişilik AI sub-team kur** — eksisi: takım büyür, artısı: net sahiplik

**Karar:**
Seçenek 3. AI Mühendisliği sub-team eklendi:
- **Agent Engineer** — custom agent / autonomous loop / multi-agent orchestration
- **Context & Workflow Engineer** — `CLAUDE.md`, `.claude/skills/`, `.claude/commands/`, `.claude/agents/`, `.claude/settings.json`, `.mcp.json`, workflow MD'leri
- **ML/RAG Engineer** — LLM, RAG, embedding, model seçimi, eval, maliyet

Dev #3 sadece "Modern Frontend" (Next.js/React/TS/edge) odağına çekildi.

**Sonuçlar:**
- (+) AI işlerinin sahibi net, eval ve güvenlik dahil end-to-end sorumluluk
- (+) Her projeye girince Context Engineer otomatik audit yapar (CLAUDE.md var mı, skill adayı var mı vs.)
- (+) SecOps'a AI-spesifik risk sinyalleri eklendi (prompt injection, model output validation, agent scope creep)
- (−) Takım büyüdü → `<team_discussion>` blokları daha karmaşık olabilir; trivial-skip kuralı sıkı uygulanmalı
- (−) Üç AI rolünün overlap'i olabilir (kim agent eval'i yapar — Agent Engineer mi QA mı?). İlk pratikte netleşecek.

**Etkilenen dosyalar:**
- `~/.claude_enterprise_team.md` (Section 1, 2, 3, 5.5 [yeni], 7)

---

## ADR-001 — Enterprise Takım İş Akışı Benimsendi

- **Tarih:** 2026-05-07
- **Durum:** Kabul edildi
- **Karar verenler:** Kullanıcı (Aykut), Tüm takım

**Bağlam:**
Tek-kişilik bir asistan yerine, kararların disiplinli şekilde alındığı (PO/Dev/QA/SecOps perspektifi), karar tarihçesi tutulan, sprint disiplini olan bir çalışma modeline geçilmek istendi.

**Karar:**
- `~/.claude_enterprise_team.md` — global takım sistem promptu (8 rol)
- `~/.claude/CLAUDE.md` — yukarıdakini import eden global Claude Code dosyası
- `.claudeteam/` — projeye özel context (opsiyonel, varsa kullanılır)

**Sonuçlar:**
- (+) Karar disiplini, tracebility, "neden böyle yaptık" sorusunun cevabı
- (+) SecOps gate'i sayesinde güvenlik ihmali azalır
- (−) Her cevapta `<team_discussion>` blok overhead'i (trivial sorular için skip kuralı eklendi)
- (−) Promptun bakım sorumluluğu kullanıcıda
