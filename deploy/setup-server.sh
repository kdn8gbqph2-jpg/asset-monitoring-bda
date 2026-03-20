#!/usr/bin/env bash
# =============================================================================
# setup-server.sh — One-time server setup for BDA Asset Monitoring
# Run as root on a fresh Ubuntu 22.04 / 24.04 VPS
# Usage: sudo bash setup-server.sh
# =============================================================================
set -euo pipefail

APP_USER="bda"
APP_DIR="/var/www/asset-monitoring"
DOMAIN="your-domain.com"   # <-- change this

echo "==> [1/7] System update"
apt-get update && apt-get upgrade -y

# ── .NET 8 Runtime ──────────────────────────────────────────────────────────
echo "==> [2/7] Installing .NET 8 Runtime"
wget -qO packages-microsoft-prod.deb \
    https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt-get update
apt-get install -y aspnetcore-runtime-8.0

# ── MySQL ────────────────────────────────────────────────────────────────────
echo "==> [3/7] Installing MySQL"
apt-get install -y mysql-server
systemctl enable --now mysql

echo "  Securing MySQL — follow the prompts..."
mysql_secure_installation

echo "  Creating database and user..."
read -rsp "Enter a password for the bda_user DB account: " DB_PASS
echo
mysql -u root -p <<SQL
CREATE DATABASE IF NOT EXISTS bda_core_prod CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'bda_user'@'localhost' IDENTIFIED BY '${DB_PASS}';
GRANT ALL PRIVILEGES ON bda_core_prod.* TO 'bda_user'@'localhost';
FLUSH PRIVILEGES;
SQL
echo "  Database bda_core_prod and user bda_user created."

# ── Nginx ────────────────────────────────────────────────────────────────────
echo "==> [4/7] Installing Nginx"
apt-get install -y nginx
systemctl enable --now nginx

# ── Certbot (SSL) ────────────────────────────────────────────────────────────
echo "==> [5/7] Installing Certbot"
apt-get install -y certbot python3-certbot-nginx
echo "  Run after DNS is pointing at this server:"
echo "  sudo certbot --nginx -d ${DOMAIN} -d www.${DOMAIN}"

# ── App user and directory ───────────────────────────────────────────────────
echo "==> [6/7] Creating app user '${APP_USER}' and directory"
id -u "${APP_USER}" &>/dev/null || useradd --system --no-create-home --shell /usr/sbin/nologin "${APP_USER}"
mkdir -p "${APP_DIR}/logs"
chown -R "${APP_USER}:${APP_USER}" "${APP_DIR}"

# ── Firewall ─────────────────────────────────────────────────────────────────
echo "==> [7/7] Configuring firewall (ufw)"
ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable

echo ""
echo "======================================================================"
echo " Setup complete! Next steps:"
echo "  1. Run deploy.sh to publish and copy the application"
echo "  2. Edit /etc/systemd/system/asset-monitoring.service"
echo "     and set the correct DB password in ConnectionStrings__DefaultConnection"
echo "  3. sudo systemctl enable --now asset-monitoring"
echo "  4. Copy nginx.conf to /etc/nginx/sites-available/asset-monitoring"
echo "     and update 'your-domain.com' in it"
echo "  5. sudo ln -s /etc/nginx/sites-available/asset-monitoring /etc/nginx/sites-enabled/"
echo "  6. sudo certbot --nginx -d ${DOMAIN} -d www.${DOMAIN}"
echo "  7. sudo nginx -t && sudo systemctl reload nginx"
echo "======================================================================"
