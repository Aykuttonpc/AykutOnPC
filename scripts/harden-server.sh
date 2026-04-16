#!/usr/bin/env bash
# ============================================================
# AykutOnPC — Hetzner VPS Hardening Script
# Run as ROOT immediately after provisioning the server.
# Usage: bash harden-server.sh
# ============================================================
set -euo pipefail

# ── CONFIG: Edit these before running ───────────────────────
NEW_USER="deploy"
YOUR_PUBLIC_KEY="ssh-ed25519 AAAAC3Nza... REPLACE_WITH_YOUR_PUBLIC_KEY"
# ────────────────────────────────────────────────────────────

echo "[1/8] Creating non-root deploy user..."
if ! id "$NEW_USER" &>/dev/null; then
    useradd -m -s /bin/bash "$NEW_USER"
fi
usermod -aG sudo "$NEW_USER"
mkdir -p /home/"$NEW_USER"/.ssh
echo "$YOUR_PUBLIC_KEY" > /home/"$NEW_USER"/.ssh/authorized_keys
chmod 700  /home/"$NEW_USER"/.ssh
chmod 600  /home/"$NEW_USER"/.ssh/authorized_keys
chown -R "$NEW_USER":"$NEW_USER" /home/"$NEW_USER"/.ssh
echo "$NEW_USER ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/"$NEW_USER"
chmod 0440 /etc/sudoers.d/"$NEW_USER"

echo "[2/8] Hardening SSH daemon..."
cat > /etc/ssh/sshd_config.d/99-hardening.conf <<'SSHEOF'
PermitRootLogin no
PasswordAuthentication no
PubkeyAuthentication yes
AuthorizedKeysFile .ssh/authorized_keys
X11Forwarding no
AllowTcpForwarding no
MaxAuthTries 3
ClientAliveInterval 300
ClientAliveCountMax 2
LoginGraceTime 30
SSHEOF
systemctl restart sshd
echo "SSH hardened. Root login DISABLED."

echo "[3/8] Updating system packages..."
apt-get update -qq && apt-get upgrade -y -qq

echo "[4/9] Installing essential tools..."
apt-get install -y -qq ufw fail2ban curl wget git unzip ca-certificates gnupg lsb-release \
    unattended-upgrades apt-listchanges auditd

echo "[5/9] Configuring UFW firewall..."
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp    comment 'SSH'
ufw allow 80/tcp    comment 'HTTP'
ufw allow 443/tcp   comment 'HTTPS'
ufw --force enable
ufw status verbose

echo "[6/9] Installing Docker Engine (official apt source)..."
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
    | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
    https://download.docker.com/linux/ubuntu \
    $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
apt-get update -qq
apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin
usermod -aG docker "$NEW_USER"
systemctl enable --now docker

echo "[7/9] Configuring Fail2ban for SSH..."
cat > /etc/fail2ban/jail.d/sshd.conf <<'F2BEOF'
[sshd]
enabled  = true
port     = ssh
filter   = sshd
logpath  = /var/log/auth.log
maxretry = 3
bantime  = 3600
findtime = 600
F2BEOF
systemctl enable --now fail2ban

echo "[8/9] Applying kernel hardening (sysctl)..."
cat > /etc/sysctl.d/99-hardening.conf <<'SYSCTLEOF'
net.ipv4.conf.all.rp_filter          = 1
net.ipv4.conf.default.rp_filter      = 1
net.ipv4.icmp_echo_ignore_broadcasts = 1
net.ipv4.conf.all.accept_source_route = 0
net.ipv6.conf.all.accept_source_route = 0
net.ipv4.conf.all.log_martians       = 1
net.ipv4.tcp_syncookies              = 1
net.ipv4.tcp_max_syn_backlog         = 2048
net.ipv6.conf.all.disable_ipv6       = 1
SYSCTLEOF
sysctl -p /etc/sysctl.d/99-hardening.conf

echo "[9/9] Enabling unattended security upgrades + auditd..."
# Auto-apply Ubuntu security patches daily; reboot only if absolutely required.
cat > /etc/apt/apt.conf.d/20auto-upgrades <<'AUEOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
AUEOF
cat > /etc/apt/apt.conf.d/50unattended-upgrades <<'UUEOF'
Unattended-Upgrade::Allowed-Origins {
    "${distro_id}:${distro_codename}-security";
    "${distro_id}ESMApps:${distro_codename}-apps-security";
    "${distro_id}ESM:${distro_codename}-infra-security";
};
Unattended-Upgrade::Package-Blacklist {
    "docker-ce";
    "docker-ce-cli";
    "containerd.io";
};
Unattended-Upgrade::AutoFixInterruptedDpkg "true";
Unattended-Upgrade::MinimalSteps "true";
Unattended-Upgrade::Remove-Unused-Kernel-Packages "true";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
Unattended-Upgrade::Automatic-Reboot "false";
Unattended-Upgrade::SyslogEnable "true";
UUEOF
systemctl enable --now unattended-upgrades
systemctl enable --now auditd

echo ""
echo "================================================================"
echo " Server hardening complete."
echo " ✅ Non-root user '$NEW_USER' created with sudo & Docker access."
echo " ✅ Root SSH login DISABLED. Password auth DISABLED."
echo " ✅ UFW: only ports 22/80/443 open."
echo " ✅ Fail2ban active. Docker installed."
echo " ✅ Unattended security upgrades enabled (Docker pinned)."
echo " ✅ auditd active for security event logging."
echo " ⚠️  CRITICAL: Test SSH as '$NEW_USER' before closing root session!"
echo "================================================================"
