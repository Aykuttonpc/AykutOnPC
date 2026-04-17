# AykutOnPC — Hetzner Sunucu Deploy Rehberi (Sıfırdan Sona, Türkçe)

> **Hedef**: Hetzner Cloud üzerinde Ubuntu 24.04 LTS sunucu kiralayıp, sertleştirip, AykutOnPC'yi (PostgreSQL + .NET 9 + Nginx + Let's Encrypt) production seviyesinde Docker ile yayına almak.
> **Önkoşul**: Hetzner hesabı (kredi kartı/PayPal ile doğrulanmış), Windows/Mac/Linux yerel makine, GitHub repo erişimi.
> **Tahmini süre**: İlk kez yapıyorsan ~2-3 saat. Sonraki seferlerde script'lerle 30 dakikaya iner.
> **İlgili dokümanlar**: [hetzner-production-guide.md](./hetzner-production-guide.md) (geniş İngilizce referans) · [runbook.md](./runbook.md) (operasyonel kılavuz, incident playbook).

---

## İçindekiler

| # | Bölüm | Süre | Yapan |
|---|---|---|---|
| 0 | Genel akış | — | — |
| 1 | Yerel makinede SSH key oluştur | 5 dk | Sen, Windows/PowerShell |
| 2 | Hetzner'da sunucu sipariş et | 10 dk | Sen, browser |
| 3 | İlk SSH ile root olarak bağlan | 5 dk | Sen, terminal |
| 4 | Sertleştirme script'ini çalıştır | 10 dk | `harden-server.sh` |
| 5 | `deploy` kullanıcısıyla bağlan & repo klonla | 10 dk | Sen, terminal |
| 6 | `.env.prod` dosyasını oluştur | 10 dk | Sen, sunucuda |
| 7 | SSL sertifikası (domain'siz / domain'li) | 5–15 dk | Certbot ya da self-signed |
| 8 | PostgreSQL'i ayağa kaldır | 5 dk | docker compose |
| 9 | Web uygulamasını başlat & migration | 5 dk | docker compose |
| 10 | İlk admin kullanıcısını seed et | 2 dk | `--seed-admin` |
| 11 | Nginx + Redis ile tüm stack'i aç | 5 dk | docker compose |
| 12 | Doğrulama (smoke test) | 10 dk | Sen, browser + curl |
| 13 | Domain bağla & gerçek SSL | 30 dk | DNS + Certbot |
| 14 | CI/CD pipeline aktif et | 10 dk | GitHub Secrets |
| 15 | Yedekleme & otomatik bakım cron'ları | 10 dk | crontab |
| 16 | Sorun giderme | her zaman | runbook'a bak |

---

## 0. Genel Akış (Diyagram)

```
[Yerel Makine]                [Hetzner Console]              [Sunucu (CX22)]
     │                              │                                │
     ├─ ssh-keygen ──────► public key                                │
     │                              │                                │
     │                              ├─ Server Add (CX22) ────────────►
     │                              │   + Ubuntu 24.04                │
     │                              │   + Public SSH key inject       │
     │                              │                                │
     │  ssh root@IP ──────────────────────────────────────────────────►
     │                                                               │
     │  scp harden-server.sh ───────────────────────────────────────► /root/
     │                                                               │
     │  ssh root@IP "bash harden-server.sh" ─────────────────────────►
     │                                                  │            │
     │                                                  ▼            │
     │                                    [deploy user + Docker + UFW + Fail2Ban]
     │                                                               │
     │  ssh deploy@IP ───────────────────────────────────────────────►
     │  git clone /opt/aykutonpc                                     │
     │  .env.prod oluştur                                            │
     │  docker compose -f docker-compose.prod.yml up -d              │
     │  --seed-admin                                                 │
     │                                                               │
     │  https://IP veya https://aykutonpc.com ◄─────────────────────┤
```

---

## 1. Yerel Makinede SSH Key Oluştur

> **Neden?** Hetzner'da root olarak parolasız bağlanmak ve sonra `deploy` kullanıcısı için aynı key'i kullanmak için. Parola tabanlı SSH'ı tamamen kapatacağız.

### Windows (PowerShell)

```powershell
# Eğer key yoksa oluştur
ssh-keygen -t ed25519 -C "aykutcincik@ogr.eskisehir.edu.tr"
# Enter, Enter (passphrase opsiyonel — koyarsan her bağlantıda sorar)
```

Üretilen dosyalar: `C:\Users\<KULLANICI>\.ssh\id_ed25519` (private — kimseye verme!) ve `id_ed25519.pub` (public — Hetzner'a yapıştıracağız).

### Public key'i kopyala (Hetzner'a yapıştırmak için)

```powershell
type $env:USERPROFILE\.ssh\id_ed25519.pub | clip
```

Şimdi panoda `ssh-ed25519 AAAA... aykutcincik@...` formatında bir string var.

### Linux/Mac

```bash
ssh-keygen -t ed25519 -C "aykutcincik@ogr.eskisehir.edu.tr"
cat ~/.ssh/id_ed25519.pub | xclip -selection clipboard   # Linux
cat ~/.ssh/id_ed25519.pub | pbcopy                       # Mac
```

---

## 2. Hetzner'da Sunucu Sipariş Et

### 2.1 Hesap & Project

1. https://console.hetzner.cloud → giriş yap
2. **+ New Project** → "AykutOnPC" adıyla aç (faturalama bu proje altında olur)

### 2.2 SSH Key'i Hetzner'a yükle

1. Sol menü → **Security** → **SSH Keys** sekmesi
2. **Add SSH Key** → panodaki public key'i yapıştır → ad ver (örn. "aykut-windows-laptop") → **Add**

### 2.3 Sunucuyu sipariş et

1. **Servers** → **+ Add Server**
2. Seçenekler:
   - **Location**: Falkenstein (FSN1) — AB / GDPR / en ucuz
   - **Image**: Ubuntu 24.04
   - **Type**: **Shared vCPU** sekmesi → **CX22** (2 vCPU AMD, 4 GB RAM, 40 GB NVMe, ~€4.51/ay)
   - **Networking**: IPv4 ✓ + IPv6 ✓ (varsayılan)
   - **SSH Keys**: az önce eklediğin key'i seç ✓
   - **Volumes**: yok (gerek yok)
   - **Firewalls**: yok (UFW'yi sunucu içinde kuracağız)
   - **Backups**: ✓ İşaretle (+%20, ~€0.90/ay — günlük snapshot)
   - **Placement Groups / Labels / Cloud config**: boş
   - **Name**: `aykutonpc-prod`
3. **Create & Buy now** → Hetzner ~30 saniyede sunucuyu ayağa kaldırır
4. Sunucunun IP'sini kopyala (örn. `5.75.123.45`)

### 2.4 (Opsiyonel) Storage Box (offsite backup için)

Domain almadan da bunu yapabilirsin:
1. Hetzner Console → **Storage Box** → **Order Storage Box** → **BX11** (1 TB, ~€3.81/ay)
2. SSH access için ek key ekle (aynı public key'i kullan)
3. Hostname'i not al: `u123456.your-storagebox.de`

---

## 3. İlk SSH ile Root Olarak Bağlan

```bash
ssh root@5.75.123.45
# İlk bağlantıda fingerprint sorar → 'yes'
```

İçeri girdiğinde şu şekilde bir prompt görmelisin:
```
Welcome to Ubuntu 24.04 LTS ...
root@aykutonpc-prod:~#
```

Sistem güncel mi diye bir kontrol:
```bash
apt-get update && apt-get upgrade -y
```

> **Önemli**: Bu pencereyi açık tut! 4. adımda sertleştirme yaparken root SSH KAPANACAK. Eğer harden script'i hatalı çalışırsa, açık root pencereniz kurtarıcınız olur.

---

## 4. Sertleştirme Script'ini Çalıştır

Repo'daki `scripts/harden-server.sh` dosyası şunları yapıyor:
- `deploy` adında non-root sudo kullanıcısı oluşturur, public key'ini ekler
- SSH'ı kilitler: root login kapalı, parola auth kapalı, 3 deneme limiti
- UFW firewall'u kurar: sadece 22/80/443
- Docker Engine + compose plugin'i resmi apt repo'sundan kurar
- Fail2Ban (SSH brute force koruması)
- Kernel sysctl sertleştirmesi (SYN cookies, ICMP redirect off, IPv6 off)
- Unattended-upgrades + auditd

### 4.1 Script'i sunucuya kopyala (yerel makineden)

İki seçenek:

**A) Repo'yu klonlayarak (önerilen)**:
```bash
# YENİ bir terminal aç (root SSH penceresi açık kalsın!)
ssh root@5.75.123.45
cd /root
apt-get install -y git
git clone https://github.com/<KULLANICI>/AykutOnPC.git
cd AykutOnPC/scripts
```

**B) `scp` ile tek dosya**:
```bash
# Yerel makineden:
scp scripts/harden-server.sh root@5.75.123.45:/root/
ssh root@5.75.123.45
cd /root
```

### 4.2 Public key'i script'in içine yaz

`harden-server.sh` ilk satırlarında `YOUR_PUBLIC_KEY` placeholder'ı var:

```bash
nano harden-server.sh
# 11. satır:
# YOUR_PUBLIC_KEY="ssh-ed25519 AAAAC3Nza... REPLACE_WITH_YOUR_PUBLIC_KEY"
```

Bunu adım 1'de kopyaladığın gerçek public key ile değiştir. `Ctrl+O`, `Enter`, `Ctrl+X` ile kaydet.

> **Bu adımı atlama!** Atlarsan `deploy` kullanıcısı oluşur ama içeri giremezsin (root SSH zaten kapanacak).

### 4.3 Script'i çalıştır

```bash
chmod +x harden-server.sh
bash harden-server.sh
```

Yaklaşık 3-5 dakika sürer. Çıktının sonunda şunu görmelisin:
```
================================================================
 Server hardening complete.
 ✅ Non-root user 'deploy' created with sudo & Docker access.
 ✅ Root SSH login DISABLED. Password auth DISABLED.
 ...
 ⚠️  CRITICAL: Test SSH as 'deploy' before closing root session!
================================================================
```

### 4.4 deploy kullanıcısıyla giriş testi (root pencereyi KAPATMA!)

YENİ bir terminal pencere açıp dene:
```bash
ssh deploy@5.75.123.45
```

Başarıyla bağlanıp `deploy@aykutonpc-prod:~$` prompt'unu gördüysen → 🎉 hardening tamam, root pencereyi kapatabilirsin.

Bağlanamadıysan → root pencereye dön, `cat /home/deploy/.ssh/authorized_keys` ile key'i kontrol et, gerekirse manuel ekle.

---

## 5. deploy Kullanıcısıyla Bağlan & Repo'yu Klonla

```bash
ssh deploy@5.75.123.45
sudo mkdir -p /opt/aykutonpc
sudo chown -R deploy:deploy /opt/aykutonpc
cd /opt/aykutonpc
git clone https://github.com/<KULLANICI>/AykutOnPC.git .
```

Repo private ise:
- GitHub Personal Access Token üret (Settings → Developer → Tokens → repo scope) — `git clone https://TOKEN@github.com/...` ile clone
- Veya bir Deploy Key oluştur ve sunucudan üreteceğin yeni bir SSH key'i (`ssh-keygen -t ed25519 -f ~/.ssh/github`) GitHub'da Deploy Key olarak ekle

Branch'i kontrol et:
```bash
git branch
# * master  veya  * main
```

Docker grup üyeliğini doğrula (harden script ekledi, ama yeni grup için tekrar login lazım):
```bash
groups | grep docker
# 'docker' yoksa: exit + tekrar ssh deploy@... ve tekrar dene
```

---

## 6. `.env.prod` Dosyasını Oluştur

Tüm secret'ları sunucuda üret. **Asla yerel makinede üretip git'e bulaştırma.**

```bash
cd /opt/aykutonpc

cat > .env.prod <<EOF
# ═══════════ Veritabanı ═══════════
DB_PASSWORD=$(openssl rand -base64 32)

# ═══════════ Redis ═══════════
REDIS_PASSWORD=$(openssl rand -base64 32)

# ═══════════ JWT (kullanıcı session imzalama) ═══════════
JWT_SECRET_KEY=$(openssl rand -base64 64)

# ═══════════ Admin kullanıcı (ilk seed için) ═══════════
ADMIN_USERNAME=aykut
ADMIN_PASSWORD=$(openssl rand -base64 24)

# ═══════════ Groq AI ═══════════
GEMINI_API_KEY=gsk_BURAYA_GROQ_PANELINDEN_ALDIGIN_KEY

# ═══════════ ASP.NET ═══════════
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
EOF

chmod 600 .env.prod
```

Şimdi içine bak ve `GEMINI_API_KEY` satırını gerçek key ile değiştir:
```bash
nano .env.prod
```

> ⚠️ `ADMIN_PASSWORD` değerini **bir yere kaydet** (parola yöneticisi). İlk login'de gerekecek. Sonra unutursan `--seed-admin` ile yenileyebilirsin.

Görüntüle ve tüm değerlerin dolu olduğundan emin ol:
```bash
cat .env.prod
```

---

## 7. SSL Sertifikası

Hangi durumdasın?

### 7.A — Henüz domainim yok (geçici self-signed cert)

Nginx TLS dinleyebilmesi için cert dosyaları lazım. Geçici bir tane üretelim:

```bash
sudo mkdir -p /etc/letsencrypt/live/aykutonpc.com
sudo openssl req -x509 -nodes -days 30 -newkey rsa:2048 \
    -keyout /etc/letsencrypt/live/aykutonpc.com/privkey.pem \
    -out /etc/letsencrypt/live/aykutonpc.com/fullchain.pem \
    -subj "/CN=$(curl -s ifconfig.me)"
```

> **Önemli**: Tarayıcı bu cert'i kabul etmez ("Bağlantınız özel değil" uyarısı verir) — bu normal. Sadece domain alana kadar bu durum sürer. Adım 13'te gerçek cert'le değiştireceğiz.

### 7.B — Domain hazır (Let's Encrypt — önerilen)

Bu adımı şimdi yapma — önce DNS ayarlanmalı. Adım 13'e atla, oraya gel ve sonra geri dön.

---

## 8. PostgreSQL'i Ayağa Kaldır

Sadece DB container'ını başlat (web henüz hazır değil):

```bash
cd /opt/aykutonpc
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d db
```

10 saniye bekle, sonra hazır mı diye sor:
```bash
sleep 10
docker compose -f docker-compose.prod.yml exec db pg_isready -U postgres
# Beklenen: /var/run/postgresql:5432 - accepting connections
```

DB'yi listele (içine girip elle bak):
```bash
docker compose -f docker-compose.prod.yml exec db psql -U postgres -l
# AykutOnPC_Db veritabanını listede görmelisin
```

> Veri kalıcı: `postgres-data` adlı Docker named volume'da tutulur. Container silinse bile veri kaybolmaz. Hetzner snapshot bu volume'u da yedekler.

---

## 9. Web Uygulamasını İlk Defa Başlat

İmajı build et + container'ı kaldır:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod build web
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d web
```

Container açılırken `Program.cs` içindeki migration loop otomatik olarak `dotnet ef database update` mantığını çalıştırır. Logları takip et:

```bash
docker compose -f docker-compose.prod.yml logs -f web
# "Now listening on: http://[::]:8080" görmelisin
# "Background: Migrations applied successfully." mesajını ara
```

`Ctrl+C` ile log takibinden çık (container çalışmaya devam eder).

---

## 10. İlk Admin Kullanıcısını Seed Et

`DbInitializer` artık yok; bunun yerine tek-seferlik bir CLI komutu var:

```bash
docker compose -f docker-compose.prod.yml exec web \
    dotnet AykutOnPC.Web.dll --seed-admin
```

Beklenen çıktı:
```
[SeedAdmin] info: Created admin user 'aykut' (Role=Admin).
```

Eğer `aykut` zaten varsa şifre yenilenir ve rol Admin olarak garantilenir.

> **Şifreyi unuttuysan**: `.env.prod` içinde yeni `ADMIN_PASSWORD` üret, web'i restart et (`docker compose ... up -d --force-recreate web`), sonra tekrar `--seed-admin` çalıştır.

---

## 11. Nginx + Redis ile Tüm Stack'i Aç

Geri kalan tüm servisleri ayağa kaldır:

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d
```

4 container hepsi `Up (healthy)` olmalı:

```bash
docker compose -f docker-compose.prod.yml ps
```

Beklenen:
```
NAME              STATUS
aykutonpc-db      Up (healthy)
aykutonpc-redis   Up (healthy)
aykutonpc-web     Up (healthy)
aykutonpc-nginx   Up (healthy)
```

---

## 12. Doğrulama (Smoke Test)

### 12.1 Health endpoint
```bash
curl -k https://localhost/health
# Beklenen: 200 OK
```

### 12.2 Tarayıcıdan kontrol
- `https://<HETZNER_IP>` (domain yoksa, self-signed uyarısı normal — devam et)
- `/Account/Login` → admin login formu (footer'daki kalkan ikonuna da tıklayabilirsin)
- `.env.prod`'daki `ADMIN_USERNAME` ve `ADMIN_PASSWORD` ile giriş

### 12.3 AI sohbeti
```bash
curl -k -X POST https://localhost/api/chat/ask \
    -H "Content-Type: application/json" \
    -d '{"message":"Aykut kimdir?"}'
# Türkçe cevap dönmeli (Groq Llama 3.3 70B)
```

### 12.4 Visitor analytics
- Sitede birkaç sayfada gez
- Admin panel → Analytics dashboard'da sayfaları gör (`/Admin/Analytics`)

---

## 13. Domain Bağla & Gerçek SSL

> Bu adım sadece domain aldıktan sonra. Almadıysan 12. adımdaki self-signed cert ile devam edebilirsin, geri dönüp burayı yaparsın.

### 13.1 DNS A record

Domain registrar'da (örn. Namecheap, Cloudflare, GoDaddy):
| Type | Host | Value | TTL |
|---|---|---|---|
| A | `@` | `5.75.123.45` (Hetzner IP) | 300 |
| A | `www` | `5.75.123.45` | 300 |

Propagation kontrolü (yerel makineden):
```bash
dig aykutonpc.com +short
# Hetzner IP'sini görmelisin (DNS yayılması 5-30 dk sürebilir)
```

### 13.2 Self-signed cert'i kaldır, Certbot ile gerçek olanı al

```bash
sudo apt install -y certbot
docker compose -f docker-compose.prod.yml stop nginx
sudo rm -rf /etc/letsencrypt/live/aykutonpc.com /etc/letsencrypt/archive/aykutonpc.com
sudo certbot certonly --standalone \
    -d aykutonpc.com -d www.aykutonpc.com \
    --email aykutcincik@ogr.eskisehir.edu.tr --agree-tos --no-eff-email
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d nginx
```

### 13.3 SSL Labs A+ doğrulaması

Tarayıcıdan: https://www.ssllabs.com/ssltest/analyze.html?d=aykutonpc.com → A+ beklenir.

### 13.4 Otomatik yenileme cron'u

```bash
sudo crontab -e
# Şu satırı ekle:
0 3 * * * certbot renew --quiet --deploy-hook "docker compose -f /opt/aykutonpc/docker-compose.prod.yml restart nginx"
```

Kaydet, çık.

---

## 14. CI/CD Pipeline Aktif Et

GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret adı | Değer |
|---|---|
| `HETZNER_HOST` | Sunucu IP veya domain (örn. `aykutonpc.com`) |
| `HETZNER_SSH_KEY` | Yerel makinendeki SSH **private key**'in tamamı (`-----BEGIN OPENSSH PRIVATE KEY-----` ile başlar, `-----END OPENSSH PRIVATE KEY-----` ile biter) |

Yerel makinede private key'i kopyalamak için:
```powershell
# Windows PowerShell:
type $env:USERPROFILE\.ssh\id_ed25519 | clip
```
```bash
# Linux/Mac:
cat ~/.ssh/id_ed25519 | xclip -selection clipboard
```

> **⚠️ Bu key'i paylaşma!** GitHub Secrets şifrelidir, yine de bir Deploy-only key üretmek daha güvenli — ama tek kişilik portföy için mevcut key yeterli.

Test:
```bash
# Yerel makineden:
git push origin master

# GitHub Actions sekmesinde "Deploy to Hetzner" workflow'unun yeşil yandığını gör
# Sunucuda:
tail -f /var/log/aykutonpc-deploy.log
```

---

## 15. Yedekleme & Otomatik Bakım Cron'ları

```bash
sudo crontab -e
# Şu satırları ekle:
0 3 * * * /opt/aykutonpc/scripts/backup-db.sh >> /var/log/aykutonpc-backup.log 2>&1
0 3 * * 0 docker system prune -af --filter "until=168h" >> /var/log/aykutonpc-cleanup.log 2>&1
```

İlk yedeği elle al:
```bash
bash /opt/aykutonpc/scripts/backup-db.sh
ls -lh /opt/aykutonpc/backups/
```

### Offsite backup (Hetzner Storage Box'a — opsiyonel ama önerilen)

```bash
sudo apt install -y rclone
rclone config
# n) New remote
# name> hetznerbox
# type> 33 (sftp)
# host> u123456.your-storagebox.de
# user> u123456
# port> 23 (Storage Box SFTP portu)
# pass> (Storage Box şifresi - Hetzner Console'dan al)
# key_file> /home/deploy/.ssh/id_ed25519 (yerine yapıştır)
# y) yes (this is OK)
# q) Quit config

# Test:
rclone lsd hetznerbox:
# Storage Box'taki klasörleri listelemeli
```

`backup-db.sh` script'i otomatik olarak rclone varsa offsite kopyalar. Test:
```bash
bash /opt/aykutonpc/scripts/backup-db.sh
# "Offsite sync OK." görmelisin
```

---

## 16. Sorun Giderme — Hızlı Komutlar

```bash
# Tüm container statüsü
docker compose -f docker-compose.prod.yml ps

# Web logları (canlı)
docker compose -f docker-compose.prod.yml logs -f web

# Sadece Nginx
docker compose -f docker-compose.prod.yml logs -f nginx

# DB içine psql
docker compose -f docker-compose.prod.yml exec db \
    psql -U postgres -d AykutOnPC_Db

# Disk doluluğu
df -h
sudo du -sh /var/lib/docker/* | sort -rh | head -5

# Bütün stack'i restart
docker compose -f docker-compose.prod.yml --env-file .env.prod restart

# Tek bir servisi yeniden inşa et
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d \
    --force-recreate --build web

# Fail2Ban'a takıldıysan (kendini yasakladıysan)
# Hetzner Console → Server → Console (web VNC) ile gir
sudo fail2ban-client set sshd unbanip <SENİN_IP>
```

Daha geniş incident playbook için → [runbook.md](./runbook.md).

---

## 17. Deploy Sonrası Checklist

- [ ] `docker compose -f docker-compose.prod.yml ps` → 4/4 healthy
- [ ] `https://<DOMAIN>/health` → 200
- [ ] Anasayfa açılıyor, footer'daki 🛡️ ikonu görünüyor
- [ ] Admin login çalışıyor (`/Account/Login`)
- [ ] AI chat cevap veriyor (Groq key geçerli)
- [ ] Analytics dashboard yükleniyor (`/Admin/Analytics`)
- [ ] `crontab -l` → backup + certbot renew cron'ları var
- [ ] GitHub Actions ilk başarılı deploy'u tamamladı
- [ ] `.env.prod` dosyası `chmod 600` ve sahibi `deploy`
- [ ] `sudo ufw status` → sadece 22 / 80 / 443 portları açık
- [ ] SSL Labs A+ aldı (domain varsa)
- [ ] `sudo systemctl is-active fail2ban` → active
- [ ] Storage Box'a offsite backup çalışıyor (`rclone listremotes` + ilk sync OK)

---

## 18. Maliyet Özeti (Aylık)

| Kalem | Tutar |
|---|---|
| Hetzner CX22 (2 vCPU / 4 GB / 40 GB NVMe) | €4.51 |
| Hetzner Backup (+%20) | €0.90 |
| Hetzner Storage Box BX11 (1 TB offsite) | €3.81 |
| Domain (.com, registrar'a göre değişir) | ~€1.00 (yıllık ~€12) |
| **TOPLAM** | **~€10/ay** |

---

_Bu rehber, deploy süreciyle birlikte güncel kalır. Yeni bir incident veya sürpriz olursa [runbook.md](./runbook.md)'a ekle ve buradaki ilgili adımı revize et._
