# Project Context

> Bu dosya `.claudeteam/` mekanizmasının parçasıdır. Her oturumda takım bunu okur.

## Temel Bilgi

- **Proje adı:** AykutOnPC
- **Sahibi / paydaş:** Aykut Çinçik (sahibi, geliştirici, içerik editörü — tek kişi)
- **Durum:** Production (Hetzner CX22'de live, CI/CD aktif)
- **Aktif branch:** master
- **URL:** aykutonpc.com (production)
- **Repo:** github.com/Aykuttonpc/AykutOnPC

## Amaç

Aykut'un dijital kimlik kartı: kişisel portfolyo + GitHub feed'li build vitrini + ziyaretçilerin sorularını yanıtlayan AI chat + içerik yönetimi için admin paneli. Hem CV/iş-başvuru aracı hem teknik vitrin (kullanılan stack'ler kendi gösterimi).

## Hedef Kullanıcı

- **Recruiter / işveren** — bio, deneyim, eğitim, projeler, iletişim
- **Geliştirici peer / network** — teknik derinlik göstergesi (chat, analytics, prod-grade deploy)
- **Aykut (admin)** — içerik CRUD, chat log review, ziyaretçi analitiği

## Başarı Kriterleri

- [ ] **Uptime ≥ %99** (Hetzner + healthcheck + CI rollback)
- [ ] **AI chat anlamlı yanıt verme oranı ≥ %80** (eval set yok henüz — Sprint #4 RAG ile gelecek)
- [ ] **Admin tüm içeriği UI'dan yönetebiliyor** (Profile/Education/Experience/Spec/KB/ChatLogs ✅, Blog Sprint #3'te)
- [ ] **Visitor analytics botları + admin'i hariç tutuyor** (✅ commit `b99519c`)
- [ ] **CV linki olarak verilebilir kalitede** (sahip subjektif kararı)

## Kısıtlar

- **Bütçe:** Hetzner CX22 (~€5/ay) + Gemini API free tier + alan adı yıllığı. Aşılmamalı.
- **Zaman:** Kişisel proje — esnek, deadline yok. Sprint disiplini opsiyonel ama Sprint Board takip ediliyor.
- **Teknik kısıtlar:**
  - .NET 9 + PostgreSQL 16 + Docker zorunlu (mevcut altyapı)
  - SemanticKernel ile OpenAI-compatible endpoint (provider değiştirilebilir — Groq → Gemini geçişi yapılmış)
  - Single-instance deploy (memory cache in-process — multi-node gerekirse Redis/distributed cache lazım)
- **Compliance:** KVKK/GDPR
  - IP'ler SHA-256 + günlük rotating salt ile hash'lenir (`PageView`, `ChatLog`)
  - Cookie sadece auth token için (analytics cookie-free)
  - Kişisel veri sadece admin-erişimli tablolarda (KB, ChatLog)

## Out of Scope

> Bu projede AÇIKÇA yapılmayacak şeyler — kapsam kayması engeli için.

- E-ticaret / ödeme akışı
- Multi-tenant / SaaS (tek admin, tek site)
- Real-time human chat (sadece AI chat)
- Mobil native uygulama (responsive web yeter)
- Müşteri/kullanıcı kayıt sistemi (admin dışında kullanıcı yok)
- Comments / forum / community (statik portfolyo + chat → bu kadar)
- Ücretli içerik / paywall
