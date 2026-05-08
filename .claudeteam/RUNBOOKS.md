# AykutOnPC — Operational Runbooks

> SRE sahibi. Bu dosya operasyonel runbook'lara index tutar.
> Her DevOps/incident işinde önce buraya bakılır, ilgili runbook açılır.

## Aktif Runbook'lar

| Runbook | Yer | Ne için | Son güncelleme |
|---|---|---|---|
| **VPS Bağlantı + Recovery** | [`D:\AYKUTONPC-VPS-REHBERI.md`](file:///D:/AYKUTONPC-VPS-REHBERI.md) | SSH, UFW lockout recovery, **IP-spesifik nft DROP recovery (yeni)**, container yönetimi, deploy, DB shell, ASLA YAPMA, hızlı tanı, multi-tenant strategy | 2026-05-08 |
| Hetzner cheatsheet | `D:\Projeler\Voxi\AykutOnPC\HETZNER-CHEATSHEET.md` | Kısa formatlı SSH/deploy komutları | (varsa) |
| Tam runbook (İngilizce) | `D:\Projeler\Voxi\AykutOnPC\docs\deployment\runbook.md` | Detaylı deploy + ops | (varsa) |
| Sıfırdan kurulum (TR) | `D:\Projeler\Voxi\AykutOnPC\docs\deployment\turkce-deploy-rehberi.md` | Yeni VPS'te baştan kurulum | (varsa) |
| Production guide | `D:\Projeler\Voxi\AykutOnPC\docs\deployment\hetzner-production-guide.md` | Production hardening | (varsa) |

---

## Hızlı Erişim — En Kritik Bilgiler

> Tam içerik için `D:\AYKUTONPC-VPS-REHBERI.md`. Bu özet sadece "şimdi acilen lazım" durumu için.

**Sunucu:** Hetzner Cloud CPX22 · `aykutonpc-prod` · IP `178.104.198.249` · Ubuntu 24.04
**SSH:** `ssh -i ~/.ssh/hetzner_aopc deploy@178.104.198.249` (key: `~/.ssh/hetzner_aopc`)
**Proje yolu:** `/opt/aykutonpc`
**Container'lar:** `aykutonpc-{db,redis,web,nginx}` (Postgres 16 + Redis 7 + .NET 9 + Nginx)
**Deploy:** `git push origin master` → GitHub Actions otomatik (manuel: `bash scripts/deploy.sh --branch master`)
**Out-of-band recovery:** Hetzner Web Console → https://console.hetzner.cloud → server → `>_ Console`

## ASLA YAPMA — AykutOnPC'ye Özel Eklemeler

Cross-project liste için template `RUNBOOKS.md` veya prompt Section 5.10. AykutOnPC'ye özel:

| ❌ Yapma | Neden | ✅ Doğrusu |
|---|---|---|
| Nginx config'i sunucuda elle düzenleyip `bash scripts/deploy.sh` | `git reset --hard` yapar, fix silinir, nginx crash | Önce git'e commit, sonra deploy |
| Hetzner Cloud Firewall ekleyip 22'yi kapatmak | Edge'de blocklar, UFW açmak fayda etmez | Cloud Firewall ekleme |
| `passwd -l root` | Web Console'a girilmez olur | Root parolayı tut |

## Şu An GEÇİCİ Durumlar

> Düzeltilmesi gereken — sırası belirsiz, runbook'tan derive edildi:

- [ ] **Nginx fix sadece sunucuda** (cert path bootstrap/ → aykutonpc.com/) — git'e commit edilmedi → `deploy.sh` çalıştırırsa siler. Domain alındıktan sonra Let's Encrypt + commit.
- [ ] **Self-signed cert** (https://178.104.198.249) — domain alınınca Certbot ile gerçek cert.
