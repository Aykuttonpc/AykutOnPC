# AykutOnPC — Hetzner Sunucu Deploy Rehberi (Türkçe)

> Hedef: Hetzner Cloud üzerinde Ubuntu 24.04 LTS sunucuya **PostgreSQL veritabanını** ve **.NET 9 Web uygulamasını** sıfırdan, üretim seviyesinde, Docker üzerinden deploy etmek.
> Önkoşul: SSH erişimi olan bir Hetzner CX22 sunucu, sunucuda `harden-server.sh` çalıştırılmış ve `deploy` kullanıcısı hazır.
> İlgili dokümanlar: [hetzner-production-guide.md](./hetzner-production-guide.md) (geniş İngilizce rehber) · [runbook.md](./runbook.md) (operasyonel kılavuz).

---

## 0. Genel Akış (Tek Bakışta)

```
1. Sunucu hazırlığı  →  harden-server.sh + Docker kurulu, deploy kullanıcısı var
2. Repo & dizin       →  /opt/aykutonpc altına git clone
3. .env.prod          →  Tüm secret'lar üretilir, dosyaya yazılır
4. SSL sertifikası    →  Domain varsa Let's Encrypt, yoksa geçici self-signed
5. DB ayağa kalkar    →  docker compose up -d db (sadece DB)
6. Migration & seed   →  Web container otomatik migrate eder, --seed-admin ile admin oluştur
7. Tüm stack          →  docker compose up -d (web + nginx + redis)
8. Doğrulama          →  /health endpoint, admin login, /api/chat testi
9. CI/CD              →  GitHub Actions secrets ekle, otomatik deploy aktif
```

---

## 1. Sunucu Önkoşulları

SSH ile bağlan:

```bash
ssh deploy@<HETZNER_IP>
```

`deploy` kullanıcısının `docker` grubunda olduğundan emin ol:

```bash
groups | grep docker
# çıktıda 'docker' geçmeli
```

Eğer Docker komutları `permission denied` veriyorsa:

```bash
sudo usermod -aG docker deploy
exit  # SSH'tan çık ve tekrar gir (grup üyeliği yeniden yüklenir)
```

---

## 2. Repo'yu Sunucuya Klonla

```bash
sudo mkdir -p /opt/aykutonpc
sudo chown -R deploy:deploy /opt/aykutonpc
cd /opt/aykutonpc
git clone https://github.com/<KULLANICI>/AykutOnPC.git .
```

> Repo private ise GitHub'da bir **Deploy Key** üret veya `gh auth login` ile token tabanlı klon yap.

Doğru branch'te olduğundan emin ol:

```bash
git branch
# * master  (ya da main — repodaki ana branch)
```

---

## 3. `.env.prod` Dosyasını Oluştur

Tüm secret'ları sunucuda üret (asla yerel makinede üretip git'e bulaştırma):

```bash
cd /opt/aykutonpc

cat > .env.prod <<EOF
# === Veritabanı ===
DB_PASSWORD=$(openssl rand -base64 32)

# === Redis ===
REDIS_PASSWORD=$(openssl rand -base64 32)

# === JWT (kullanıcı session imzalama) ===
JWT_SECRET_KEY=$(openssl rand -base64 64)

# === Admin kullanıcı (ilk seed için) ===
ADMIN_USERNAME=aykut
ADMIN_PASSWORD=$(openssl rand -base64 24)

# === Groq AI ===
GEMINI_API_KEY=gsk_BURAYA_GROQ_PANELINDEN_ALDIGIN_KEY

# === ASP.NET ===
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
EOF

chmod 600 .env.prod
```

> ⚠️ `ADMIN_PASSWORD` değerini bir yere kaydet. İlk login'de gerekecek.
> ⚠️ `GEMINI_API_KEY` satırını manuel olarak Groq Console'dan aldığın key ile değiştir.

Görüntüle (kopyala/sakla):

```bash
cat .env.prod
```

---

## 4. SSL Sertifikası Hazırlığı

### 4.A — Domain Henüz Yoksa (Geçici self-signed)

Nginx'in TLS dinleyebilmesi için boş da olsa cert dosyaları lazım:

```bash
sudo mkdir -p /etc/letsencrypt/live/temp
sudo openssl req -x509 -nodes -days 30 -newkey rsa:2048 \
    -keyout /etc/letsencrypt/live/temp/privkey.pem \
    -out /etc/letsencrypt/live/temp/fullchain.pem \
    -subj "/CN=$(curl -s ifconfig.me)"
```

`nginx/conf.d/aykutonpc.conf` içindeki `ssl_certificate` yolunu geçici olarak `/etc/letsencrypt/live/temp/` altını gösterecek şekilde değiştir (sadece bu geçici aşamada).

### 4.B — Domain Hazır (Önerilen, Let's Encrypt)

```bash
sudo apt install -y certbot
sudo certbot certonly --standalone \
    -d aykutonpc.com -d www.aykutonpc.com \
    --email aykutcincik@ogr.eskisehir.edu.tr --agree-tos --no-eff-email
```

Cert dosyaları otomatik olarak `/etc/letsencrypt/live/aykutonpc.com/` altına gider. `docker-compose.prod.yml` zaten bu dizini Nginx'e read-only mount ediyor.

**Otomatik yenileme cron'u**:

```bash
sudo crontab -e
# Aşağıdaki satırı ekle:
0 3 * * * certbot renew --quiet --deploy-hook "docker compose -f /opt/aykutonpc/docker-compose.prod.yml restart nginx"
```

---

## 5. PostgreSQL'i Ayağa Kaldır

Sadece veritabanı container'ını başlat (web henüz hazır değil):

```bash
cd /opt/aykutonpc
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d db
```

Sağlık kontrolü:

```bash
# 10 saniye bekle, sonra hazır mı diye sor
sleep 10
docker compose -f docker-compose.prod.yml exec db pg_isready -U postgres
# Beklenen: /var/run/postgresql:5432 - accepting connections
```

DB'nin içine girip elle bak (opsiyonel):

```bash
docker compose -f docker-compose.prod.yml exec db psql -U postgres -l
# AykutOnPC_Db veritabanını listede görmelisin (init script'i yarattı)
```

> Veri kalıcı: `postgres-data` adlı Docker named volume'da tutulur. Container silinse bile veri kaybolmaz. Hetzner snapshot bu volume'u da yedekler.

---

## 6. Web Container'ını İlk Defa Başlat

İmajı build et + container'ı kaldır (henüz nginx başlatma):

```bash
docker compose -f docker-compose.prod.yml --env-file .env.prod build web
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d web
```

Container açılırken `Program.cs` içindeki migration loop otomatik olarak `dotnet ef database update` mantığını çalıştırır. Logları takip et:

```bash
docker compose -f docker-compose.prod.yml logs -f web
# "Now listening on: http://[::]:8080" görmelisin
# Migration mesajlarına dikkat et: "Applied migration: AddVisitorIntelligence"
```

`Ctrl+C` ile log takibinden çık.

---

## 7. İlk Admin Kullanıcısını Oluştur

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

> Şifreyi unuttuysan: `.env.prod` içinde yeni `ADMIN_PASSWORD` üret, web'i restart et, sonra tekrar `--seed-admin` çalıştır.

---

## 8. Nginx + Redis'i Aç (Tüm Stack)

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

## 9. Doğrulama (Smoke Test)

### 9.1 Health endpoint
```bash
curl -k https://localhost/health
# Beklenen: 200 OK, JSON body
```

### 9.2 Tarayıcıdan kontrol
- `https://<HETZNER_IP>` (domain yoksa, self-signed uyarısı normal)
- `https://aykutonpc.com` (domain varsa, kilit ikonu görünmeli)
- `https://aykutonpc.com/admin` → admin login formu

### 9.3 Admin login
- Username: `.env.prod` içindeki `ADMIN_USERNAME` (varsayılan `aykut`)
- Password: `.env.prod` içindeki `ADMIN_PASSWORD`

### 9.4 AI sohbeti
```bash
curl -k -X POST https://localhost/api/chat \
    -H "Content-Type: application/json" \
    -d '{"message":"Aykut kimdir?"}'
# Türkçe bir cevap dönmeli (Groq Llama 3.3 70B)
```

### 9.5 Visitor analytics
- Sitede birkaç sayfada gezin
- Admin paneli → Analytics dashboard'da sayfaları gör

---

## 10. CI/CD Aktif Et (GitHub Actions)

GitHub repo → **Settings** → **Secrets and variables** → **Actions**:

| Secret adı | Değer |
|---|---|
| `HETZNER_HOST` | Sunucu IP (örn. `5.75.123.45`) |
| `HETZNER_SSH_KEY` | Lokal'den SSH private key tamamı (`-----BEGIN ... -----END`) |

Sunucuda `deploy` kullanıcısının `~/.ssh/authorized_keys` içine bu key'in **public** versiyonu eklenmiş olmalı (zaten `harden-server.sh` ekliyor).

Test:
```bash
# Lokal makineden
git push origin master
# GitHub Actions sekmesinde "Deploy to Hetzner" workflow'unun yeşil yandığını gör
# Sunucuda:
tail -f /var/log/aykutonpc-deploy.log
```

---

## 11. Yedekleme Otomasyonu (Cron)

`scripts/backup-db.sh` her gece 03:00'te çalışacak:

```bash
sudo crontab -e
# Aşağıdaki iki satırı ekle:
0 3 * * * /opt/aykutonpc/scripts/backup-db.sh >> /var/log/aykutonpc-backup.log 2>&1
0 3 * * 0 docker system prune -af --filter "until=168h" >> /var/log/aykutonpc-cleanup.log 2>&1
```

İlk yedeği elle al ve sonucu gör:

```bash
bash /opt/aykutonpc/scripts/backup-db.sh
ls -lh /opt/aykutonpc/backups/
```

> **Offsite backup** (Hetzner Storage Box): `rclone config` ile `hetznerbox` adında SFTP remote tanımla. `backup-db.sh` script'i otomatik olarak rclone varsa offsite kopyalar.

---

## 12. Sorun Giderme — Hızlı Komutlar

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

# Bütün stack'i restart et
docker compose -f docker-compose.prod.yml --env-file .env.prod restart

# Acil durum: tek bir servisi yeniden inşa et
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d \
    --force-recreate --build web
```

Daha geniş incident playbook için → [runbook.md](./runbook.md).

---

## 13. Kapatma Listesi (İlk Deploy Sonrası)

- [ ] `docker compose ps` → 4/4 healthy
- [ ] `https://<DOMAIN>/health` → 200
- [ ] Admin login çalışıyor
- [ ] AI chat cevap veriyor (Groq key geçerli)
- [ ] `crontab -l` → backup cron mevcut
- [ ] `crontab -l` → certbot renew cron mevcut (domain varsa)
- [ ] GitHub Actions ilk başarılı deploy'u tamamladı
- [ ] `.env.prod` dosyası `chmod 600` ve sahibi `deploy`
- [ ] Sunucuda **sadece** 22 / 80 / 443 portları açık (`sudo ufw status`)

---

_Bu rehber, aktif deploy süreciyle birlikte güncellenecektir. Her yeni incident veya sürpriz, bir öğrenmedir — runbook'a ekle._
